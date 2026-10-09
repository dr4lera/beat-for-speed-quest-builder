package com.zrock.bfsquest;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

public class SongPicker extends Fragment {
    private String target;
    private static final int REQUEST = 4413;
    public static void launch(final String objectName) {
        final Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            SongPicker picker = new SongPicker();
            Bundle args = new Bundle(); args.putString("target", objectName); picker.setArguments(args);
            activity.getFragmentManager().beginTransaction().add(picker, "BFSSongPicker").commitAllowingStateLoss();
        });
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state); target = getArguments().getString("target");
        if (state != null) return;
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE); intent.setType("*/*");
        intent.putExtra(Intent.EXTRA_LOCAL_ONLY, true);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        try { startActivityForResult(intent, REQUEST); }
        catch (Exception error) { finish("ERROR:No system file picker is available. Use the USB Imports folder."); }
    }
    @Override public void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != REQUEST) return;
        if (result != Activity.RESULT_OK || data == null || data.getData() == null) { finish("CANCELLED"); return; }
        final Uri uri = data.getData(); final Activity activity = getActivity();
        new Thread(() -> {
            File temporary = null;
            try {
                String name = "";
                try (Cursor cursor = activity.getContentResolver().query(uri, null, null, null, null)) {
                    if (cursor != null && cursor.moveToFirst()) {
                        int index = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                        if (index >= 0) name = cursor.getString(index);
                    }
                }
                if (name == null || !name.toLowerCase(java.util.Locale.ROOT).endsWith(".bfs"))
                    throw new Exception("Choose a .bfs song pack containing chart.json and audio.");
                String safe = name.replaceAll("[^A-Za-z0-9._ -]", "_");
                if (safe.length() > 120) safe = safe.substring(0, 110) + ".bfs";
                File folder = new File(activity.getExternalFilesDir(null), "Imports");
                if (!folder.exists() && !folder.mkdirs()) throw new Exception("Cannot create song folder");
                String unique = System.currentTimeMillis() + "-" + safe;
                temporary = new File(folder, unique + ".partial");
                try (InputStream in = activity.getContentResolver().openInputStream(uri);
                     FileOutputStream out = new FileOutputStream(temporary)) {
                    if (in == null) throw new Exception("Cannot open selected file");
                    byte[] buffer = new byte[65536]; int read; long total = 0;
                    while ((read = in.read(buffer)) != -1) {
                        total += read;
                        if (total > 272L * 1024 * 1024) throw new Exception("Song pack exceeds 272 MB");
                        out.write(buffer, 0, read);
                    }
                }
                File destination = new File(folder, unique);
                if (!temporary.renameTo(destination)) throw new Exception("Cannot save selected song");
                finish("OK:" + destination.getName());
            } catch (Exception error) {
                if (temporary != null) temporary.delete();
                finish("ERROR:" + error.getMessage());
            }
        }, "BFSSongImport").start();
    }
    private void finish(final String message) {
        UnityPlayer.UnitySendMessage(target, "OnSongPicked", message);
        final Activity activity = getActivity();
        if (activity != null) activity.runOnUiThread(() -> {
            if (isAdded()) activity.getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
        });
    }
}
