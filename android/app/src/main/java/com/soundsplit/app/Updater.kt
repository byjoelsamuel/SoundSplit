package com.soundsplit.app

import android.app.DownloadManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Environment
import android.provider.Settings
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.io.File
import java.net.HttpURLConnection
import java.net.URL

/** Checks GitHub Releases for a newer APK and installs it. */
object Updater {
    private const val LATEST = "https://api.github.com/repos/byjoelsamuel/SoundSplit/releases/latest"
    private const val APK_MIME = "application/vnd.android.package-archive"

    data class Release(val version: String, val apkUrl: String, val notes: String)

    sealed interface Download {
        data class Running(val fraction: Float) : Download
        data object Done : Download
        data object Failed : Download
    }

    /** Returns the latest release if it's newer than [current], otherwise null. Never throws. */
    suspend fun check(current: String): Release? = withContext(Dispatchers.IO) {
        try {
            val conn = (URL(LATEST).openConnection() as HttpURLConnection).apply {
                setRequestProperty("Accept", "application/vnd.github+json")
                setRequestProperty("User-Agent", "SoundSplit-Android")
                connectTimeout = 8000; readTimeout = 8000
            }
            conn.use {
                if (it.responseCode != 200) return@withContext null
                val json = JSONObject(it.inputStream.bufferedReader().readText())
                val tag = json.getString("tag_name")
                if (!Versions.isNewer(tag, current)) return@withContext null
                val assets = json.getJSONArray("assets")
                val apk = (0 until assets.length()).map { i -> assets.getJSONObject(i) }
                    .firstOrNull { a -> a.getString("name").endsWith(".apk") } ?: return@withContext null
                Release(Versions.clean(tag), apk.getString("browser_download_url"), json.optString("body"))
            }
        } catch (_: Exception) { null }
    }

    private inline fun <T> HttpURLConnection.use(block: (HttpURLConnection) -> T): T =
        try { block(this) } finally { disconnect() }

    fun startDownload(context: Context, release: Release): Long {
        val name = "SoundSplit-${release.version}.apk"
        File(context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), name).delete()
        val request = DownloadManager.Request(Uri.parse(release.apkUrl))
            .setTitle("SoundSplit ${release.version}")
            .setDescription("Downloading update")
            .setMimeType(APK_MIME)
            .setDestinationInExternalFilesDir(context, Environment.DIRECTORY_DOWNLOADS, name)
            .setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE)
        return context.getSystemService(DownloadManager::class.java).enqueue(request)
    }

    fun progress(context: Context, id: Long): Download {
        val dm = context.getSystemService(DownloadManager::class.java)
        dm.query(DownloadManager.Query().setFilterById(id)).use { c ->
            if (!c.moveToFirst()) return Download.Failed
            return when (c.getInt(c.getColumnIndexOrThrow(DownloadManager.COLUMN_STATUS))) {
                DownloadManager.STATUS_SUCCESSFUL -> Download.Done
                DownloadManager.STATUS_FAILED -> Download.Failed
                else -> {
                    val total = c.getLong(c.getColumnIndexOrThrow(DownloadManager.COLUMN_TOTAL_SIZE_BYTES))
                    val done = c.getLong(c.getColumnIndexOrThrow(DownloadManager.COLUMN_BYTES_DOWNLOADED_SO_FAR))
                    Download.Running(if (total > 0) done.toFloat() / total else 0f)
                }
            }
        }
    }

    /**
     * Opens the system installer. Android first asks the user to allow SoundSplit to install
     * apps; returns false (and opens that settings page) if that hasn't been allowed yet.
     */
    fun install(context: Context, id: Long): Boolean {
        if (!context.packageManager.canRequestPackageInstalls()) {
            context.startActivity(Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${context.packageName}"))
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
            return false
        }
        val uri = context.getSystemService(DownloadManager::class.java).getUriForDownloadedFile(id) ?: return false
        context.startActivity(Intent(Intent.ACTION_VIEW).setDataAndType(uri, APK_MIME)
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK))
        return true
    }
}

/** Version comparison kept free of Android types so it can be unit-tested on the JVM. */
object Versions {
    fun clean(tag: String): String = tag.trim().removePrefix("v").removePrefix("V")

    private fun parts(v: String): List<Int>? =
        clean(v).substringBefore('-').split('.').map { it.toIntOrNull() ?: return null }

    /** True only if [candidate] is a strictly higher version than [current] ("1.10.0" > "1.9.0"). */
    fun isNewer(candidate: String, current: String): Boolean {
        val a = parts(candidate) ?: return false
        val b = parts(current) ?: return false
        for (i in 0 until maxOf(a.size, b.size)) {
            val x = a.getOrElse(i) { 0 }; val y = b.getOrElse(i) { 0 }
            if (x != y) return x > y
        }
        return false
    }
}
