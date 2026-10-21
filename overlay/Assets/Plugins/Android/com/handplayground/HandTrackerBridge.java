package com.handplayground;

import android.app.Activity;
import android.content.Context;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraManager;
import android.os.SystemClock;
import android.util.Log;
import android.util.Size;
import android.util.SizeF;
import android.view.Surface;

import androidx.annotation.NonNull;
import androidx.camera.core.CameraSelector;
import androidx.camera.core.ImageAnalysis;
import androidx.camera.core.ImageProxy;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.core.content.ContextCompat;
import androidx.lifecycle.Lifecycle;
import androidx.lifecycle.LifecycleOwner;
import androidx.lifecycle.LifecycleRegistry;

import com.google.common.util.concurrent.ListenableFuture;
import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.components.containers.Category;
import com.google.mediapipe.tasks.components.containers.Landmark;
import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.google.mediapipe.tasks.core.BaseOptions;
import com.google.mediapipe.tasks.core.Delegate;
import com.google.mediapipe.tasks.vision.core.ImageProcessingOptions;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarker;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarkerResult;

import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * CameraX (rear camera, RGBA, KEEP_ONLY_LATEST) -> MediaPipe Tasks HandLandmarker (LIVE_STREAM, async).
 * Unity polls {@link #getFrameId()} (cheap) and fetches {@link #getSnapshot()} only when it changed.
 * Snapshot floats: [0]frameId [1]status [2]tsMs [3]inferMs [4]fovH [5]fovV [6]aspect [7]fps [8,9]rsv,
 * then 2 slots x 130: present,label(0 Left/1 Right),score,pad, 21*3 normalized lm, 21*3 world lm.
 */
public class HandTrackerBridge implements LifecycleOwner {
    private static final String TAG = "HandTrackerBridge";
    private static final String MODEL = "hand_landmarker.task"; // StreamingAssets -> APK assets
    public static final int ST_STOPPED = 0, ST_STARTING = 1, ST_RUNNING = 2, ST_CAMERA_ERR = 3, ST_MODEL_ERR = 4, ST_PERMISSION = 5;
    private static final int HDR = 10, SLOT = 130;

    private final Activity activity;
    private final Object lock = new Object();
    private final float[] snap = new float[HDR + 2 * SLOT];
    private final java.util.concurrent.atomic.AtomicInteger frameCounter = new java.util.concurrent.atomic.AtomicInteger();
    private volatile int status = ST_STOPPED;

    private LifecycleRegistry registry;
    private ProcessCameraProvider provider;
    private HandLandmarker landmarker;
    private ExecutorService analysisExec;
    private final Bitmap[] ring = new Bitmap[3];
    private int ringIdx = 0;
    private long startMs, lastAnalyzed = 0;
    private int minIntervalMs = 33, targetW = 640, targetH = 480;
    private float fovH = 65f, fovV = 50f, fpsEma = 0f;
    private long lastResultMs = 0;
    private volatile boolean running = false;

    public HandTrackerBridge(Activity a) { this.activity = a; }

    @NonNull @Override public Lifecycle getLifecycle() { return registry; }
    public int getFrameId() { return frameCounter.get(); }

    public float[] getSnapshot() {
        synchronized (lock) { return snap.clone(); }
    }

    private void setStatus(int s) { status = s; synchronized (lock) { snap[1] = s; } frameCounter.incrementAndGet(); }

    public void start(final int width, final int height, final boolean useGpu, final int maxFps,
                      final int numHands, final float det, final float pres, final float trk) {
        if (running) return;
        running = true; startMs = SystemClock.elapsedRealtime();
        targetW = width; targetH = height; minIntervalMs = Math.max(1, 1000 / Math.max(1, maxFps));
        setStatus(ST_STARTING);
        if (activity.checkSelfPermission(android.Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
            // Unity requests the permission; retry shortly.
            setStatus(ST_PERMISSION);
            activity.getWindow().getDecorView().postDelayed(new Runnable() { public void run() {
                running = false; start(width, height, useGpu, maxFps, numHands, det, pres, trk); } }, 1000);
            return;
        }
        analysisExec = Executors.newSingleThreadExecutor();
        try {
            BaseOptions.Builder bo = BaseOptions.builder().setModelAssetPath(MODEL);
            bo.setDelegate(useGpu ? Delegate.GPU : Delegate.CPU);
            HandLandmarker.HandLandmarkerOptions opts = HandLandmarker.HandLandmarkerOptions.builder()
                .setBaseOptions(bo.build()).setNumHands(numHands)
                .setMinHandDetectionConfidence(det).setMinHandPresenceConfidence(pres).setMinTrackingConfidence(trk)
                .setRunningMode(RunningMode.LIVE_STREAM)
                .setResultListener((result, input) -> onResult(result))
                .setErrorListener(e -> Log.e(TAG, "MediaPipe error", e)).build();
            landmarker = HandLandmarker.createFromOptions(activity, opts);
        } catch (Throwable t) {
            Log.e(TAG, "Model init failed (is hand_landmarker.task in StreamingAssets? GPU unsupported?)", t);
            setStatus(ST_MODEL_ERR); running = false; return;
        }
        computeFov();
        activity.runOnUiThread(this::bindCamera);
    }

    private void computeFov() {
        try {
            CameraManager cm = (CameraManager) activity.getSystemService(Context.CAMERA_SERVICE);
            for (String id : cm.getCameraIdList()) {
                CameraCharacteristics cc = cm.getCameraCharacteristics(id);
                Integer facing = cc.get(CameraCharacteristics.LENS_FACING);
                if (facing == null || facing != CameraCharacteristics.LENS_FACING_BACK) continue;
                SizeF size = cc.get(CameraCharacteristics.SENSOR_INFO_PHYSICAL_SIZE);
                float[] fl = cc.get(CameraCharacteristics.LENS_INFO_AVAILABLE_FOCAL_LENGTHS);
                if (size == null || fl == null || fl.length == 0) break;
                float lng = Math.max(size.getWidth(), size.getHeight()), sht = Math.min(size.getWidth(), size.getHeight());
                // Analysis is cropped to targetW:targetH inside the sensor; approximate with the long side as width.
                double aspect = (double) targetW / targetH, sensorAspect = lng / sht;
                float w = lng, h = sht;
                if (aspect < sensorAspect) w = (float) (sht * aspect); else h = (float) (lng / aspect);
                fovH = (float) Math.toDegrees(2 * Math.atan(w / (2 * fl[0])));
                fovV = (float) Math.toDegrees(2 * Math.atan(h / (2 * fl[0])));
                Log.i(TAG, "Rear camera FOV " + fovH + " x " + fovV);
                break;
            }
        } catch (Throwable t) { Log.w(TAG, "FOV fallback (65x50): " + t); }
    }

    private void bindCamera() {
        try {
            registry = new LifecycleRegistry(this);
            registry.setCurrentState(Lifecycle.State.STARTED);
            final ListenableFuture<ProcessCameraProvider> f = ProcessCameraProvider.getInstance(activity);
            f.addListener(() -> {
                try {
                    provider = f.get();
                    int rot = activity.getWindowManager().getDefaultDisplay().getRotation();
                    ImageAnalysis ia = new ImageAnalysis.Builder()
                        .setTargetResolution(new Size(targetW, targetH))
                        .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                        .setOutputImageFormat(ImageAnalysis.OUTPUT_IMAGE_FORMAT_RGBA_8888)
                        .setTargetRotation(rot).build();
                    ia.setAnalyzer(analysisExec, this::analyze);
                    provider.unbindAll();
                    provider.bindToLifecycle(this, CameraSelector.DEFAULT_BACK_CAMERA, ia);
                    setStatus(ST_RUNNING);
                } catch (Throwable t) { Log.e(TAG, "Camera bind failed", t); setStatus(ST_CAMERA_ERR); }
            }, ContextCompat.getMainExecutor(activity));
        } catch (Throwable t) { Log.e(TAG, "Camera init failed", t); setStatus(ST_CAMERA_ERR); }
    }

    private void analyze(ImageProxy image) {
        try {
            long now = SystemClock.elapsedRealtime();
            if (!running || landmarker == null || now - lastAnalyzed < minIntervalMs) return;
            lastAnalyzed = now;
            int w = image.getWidth(), h = image.getHeight();
            Bitmap bmp = ring[ringIdx];
            if (bmp == null || bmp.getWidth() != w || bmp.getHeight() != h) { bmp = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888); ring[ringIdx] = bmp; }
            ringIdx = (ringIdx + 1) % ring.length;
            java.nio.ByteBuffer buf = image.getPlanes()[0].getBuffer(); buf.rewind();
            int rs = image.getPlanes()[0].getRowStride();
            if (rs == w * 4) bmp.copyPixelsFromBuffer(buf);
            else { int[] row = new int[w]; byte[] rb = new byte[rs]; int[] px = new int[w * h];
                for (int y = 0; y < h; y++) { buf.position(y * rs); buf.get(rb, 0, Math.min(rs, buf.remaining()));
                    for (int x = 0; x < w; x++) { int o = x * 4; px[y * w + x] = ((rb[o + 3] & 255) << 24) | ((rb[o] & 255) << 16) | ((rb[o + 1] & 255) << 8) | (rb[o + 2] & 255); } }
                bmp.setPixels(px, 0, w, 0, 0, w, h); }
            int rot = image.getImageInfo().getRotationDegrees();
            ImageProcessingOptions ipo = ImageProcessingOptions.builder().setRotationDegrees(rot).build();
            boolean swap = rot == 90 || rot == 270;
            synchronized (lock) { snap[6] = swap ? (float) h / w : (float) w / h; }
            MPImage mp = new BitmapImageBuilder(bmp).build();
            landmarker.detectAsync(mp, ipo, now - startMs);
        } catch (Throwable t) { Log.e(TAG, "analyze", t); }
        finally { image.close(); }
    }

    private void onResult(HandLandmarkerResult r) {
        long now = SystemClock.elapsedRealtime();
        List<List<NormalizedLandmark>> lms = r.landmarks();
        List<List<Landmark>> wl = r.worldLandmarks();
        List<List<Category>> hd = r.handedness();
        synchronized (lock) {
            long dt = lastResultMs == 0 ? 33 : Math.max(1, now - lastResultMs); lastResultMs = now;
            fpsEma = fpsEma == 0 ? 1000f / dt : fpsEma * 0.9f + 100f / dt;
            snap[0] = frameCounter.get() + 1; snap[1] = status; snap[2] = now - startMs;
            snap[3] = Math.max(0, now - startMs - r.timestampMs()); snap[4] = fovH; snap[5] = fovV; snap[7] = fpsEma;
            for (int s = 0; s < 2; s++) {
                int o = HDR + s * SLOT;
                if (s >= lms.size() || lms.get(s).size() < 21 || s >= wl.size() || wl.get(s).size() < 21 || s >= hd.size() || hd.get(s).isEmpty()) { snap[o] = 0; continue; }
                snap[o] = 1;
                Category c = hd.get(s).get(0);
                snap[o + 1] = "Left".equals(c.categoryName()) ? 0 : 1; snap[o + 2] = c.score();
                for (int i = 0; i < 21; i++) {
                    NormalizedLandmark n = lms.get(s).get(i); Landmark w = wl.get(s).get(i);
                    int a = o + 4 + i * 3, b = o + 4 + 63 + i * 3;
                    snap[a] = n.x(); snap[a + 1] = n.y(); snap[a + 2] = n.z();
                    snap[b] = w.x(); snap[b + 1] = w.y(); snap[b + 2] = w.z();
                }
            }
        }
        frameCounter.incrementAndGet();
    }

    public void stop() {
        running = false;
        activity.runOnUiThread(() -> {
            try { if (provider != null) provider.unbindAll(); } catch (Throwable ignored) {}
            if (registry != null) { try { registry.setCurrentState(Lifecycle.State.DESTROYED); } catch (Throwable ignored) {} }
        });
        try { if (landmarker != null) landmarker.close(); } catch (Throwable ignored) {}
        landmarker = null;
        if (analysisExec != null) analysisExec.shutdown();
        setStatus(ST_STOPPED);
    }
}
