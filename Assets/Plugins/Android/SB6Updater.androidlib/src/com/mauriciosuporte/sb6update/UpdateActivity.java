package com.mauriciosuporte.sb6update;

import android.app.Activity;
import android.app.ActivityOptions;
import android.app.AlertDialog;
import android.app.PendingIntent;
import android.content.Intent;
import android.content.pm.PackageInstaller;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import java.io.File;
import java.io.FileInputStream;
import java.io.OutputStream;

/** Owns installation independently of Unity, which exits once this activity starts. */
public final class UpdateActivity extends Activity {
    public static boolean install(Activity game, String path) {
        if (Build.VERSION.SDK_INT >= 26 && !game.getPackageManager().canRequestPackageInstalls()) {
            game.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                    Uri.parse("package:" + game.getPackageName())));
            return false;
        }
        game.startActivity(new Intent(game, UpdateActivity.class).putExtra("apk", path));
        return true;
    }

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        handle(getIntent());
    }
    @Override public void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        handle(intent);
    }
    private void reopen() {
        Intent launch = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (launch != null) startActivity(launch.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP));
        finish();
    }
    private void handle(Intent intent) {
        if (intent.hasExtra(PackageInstaller.EXTRA_STATUS)) {
            int status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE);
            if (status == PackageInstaller.STATUS_PENDING_USER_ACTION) {
                // The legacy overload is required on the supported Android 8-12 devices.
                Intent confirm = Build.VERSION.SDK_INT >= 33 ? intent.getParcelableExtra(Intent.EXTRA_INTENT, Intent.class) : confirmationBefore33(intent);
                if (confirm != null) startActivity(confirm);
            } else if (status == PackageInstaller.STATUS_SUCCESS) {
                new File(getCacheDir(), "SB6-update.apk").delete();
                reopen();
            } else showError(intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE));
            return;
        }
        String path = intent.getStringExtra("apk");
        if (path == null) { reopen(); return; }
        // Do not install twice if Android recreates the activity.
        intent.removeExtra("apk");
        new Thread(() -> {
            PackageInstaller.Session session = null;
            try {
                File apk = new File(path);
                PackageInstaller installer = getPackageManager().getPackageInstaller();
                PackageInstaller.SessionParams params = new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
                params.setAppPackageName(getPackageName());
                params.setSize(apk.length());
                int id = installer.createSession(params);
                session = installer.openSession(id);
                try (FileInputStream input = new FileInputStream(apk);
                     OutputStream output = session.openWrite("SB6-update.apk", 0, apk.length())) {
                    byte[] buffer = new byte[65536];
                    int count;
                    while ((count = input.read(buffer)) > 0) output.write(buffer, 0, count);
                    session.fsync(output);
                }
                Intent callback = new Intent(this, UpdateActivity.class).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP);
                int flags = PendingIntent.FLAG_UPDATE_CURRENT;
                if (Build.VERSION.SDK_INT >= 31) flags |= PendingIntent.FLAG_MUTABLE;
                ActivityOptions options = ActivityOptions.makeBasic();
                if (Build.VERSION.SDK_INT >= 36)
                    options.setPendingIntentCreatorBackgroundActivityStartMode(ActivityOptions.MODE_BACKGROUND_ACTIVITY_START_ALLOW_ALWAYS);
                else if (Build.VERSION.SDK_INT == 35)
                    // API 35's MODE_BACKGROUND_ACTIVITY_START_ALLOWED (1) was replaced in API 36.
                    options.setPendingIntentCreatorBackgroundActivityStartMode(1);
                PendingIntent result = PendingIntent.getActivity(this, id, callback, flags, options.toBundle());
                session.commit(result.getIntentSender());
            } catch (Exception error) {
                if (session != null) session.abandon();
                runOnUiThread(() -> showError(error.getMessage()));
            } finally { if (session != null) session.close(); }
        }, "SB6-install").start();
    }
    private void showError(String detail) {
        new AlertDialog.Builder(this).setTitle("Super Bomberman 6")
                .setMessage("Update failed / Falha ao atualizar / Error al actualizar / 更新に失敗しました\n" + (detail == null ? "" : detail))
                .setPositiveButton(android.R.string.ok, (dialog, which) -> reopen()).setOnCancelListener(dialog -> reopen()).show();
    }
    private static Intent confirmationBefore33(Intent intent) {
        try { return (Intent) Intent.class.getMethod("getParcelableExtra", String.class).invoke(intent, Intent.EXTRA_INTENT); }
        catch (ReflectiveOperationException error) { throw new IllegalStateException("Cannot open installer confirmation", error); }
    }
}
