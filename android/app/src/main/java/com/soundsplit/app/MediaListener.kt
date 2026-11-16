package com.soundsplit.app

import android.app.PendingIntent
import android.content.ComponentName
import android.content.Context
import android.graphics.Bitmap
import android.media.MediaMetadata
import android.media.session.MediaController
import android.media.session.MediaSessionManager
import android.media.session.PlaybackState
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.service.notification.NotificationListenerService
import androidx.core.app.NotificationManagerCompat
import kotlinx.coroutines.flow.MutableStateFlow

/** What's playing right now, in whatever app (Brave/YouTube Music, Spotify, …). */
data class Track(
    val title: String,
    val artist: String,
    val art: Bitmap?,
    val durationMs: Long,
    val positionMs: Long,
    val playing: Boolean,
    val appPackage: String,
    val openApp: PendingIntent?,
)

/**
 * Android only shares other apps' media sessions with apps that have notification access.
 * This listener never reads notifications; it just follows the active media session and
 * pushes title, artist, cover and position to the home-screen widget.
 */
class MediaListener : NotificationListenerService() {

    companion object {
        val track = MutableStateFlow<Track?>(null)
        @Volatile var controller: MediaController? = null
            private set

        fun component(context: Context) = ComponentName(context, MediaListener::class.java)

        fun isEnabled(context: Context) =
            NotificationManagerCompat.getEnabledListenerPackages(context).contains(context.packageName)
    }

    private lateinit var sessions: MediaSessionManager
    private val handler = Handler(Looper.getMainLooper())
    private var artSource: Bitmap? = null
    private var artScaled: Bitmap? = null
    private var lastKey: String? = null

    private val sessionsChanged = MediaSessionManager.OnActiveSessionsChangedListener { list -> pick(list.orEmpty()) }

    private val callback = object : MediaController.Callback() {
        override fun onPlaybackStateChanged(state: PlaybackState?) = render()
        override fun onMetadataChanged(metadata: MediaMetadata?) = render()
        override fun onSessionDestroyed() = pick(sessions.getActiveSessions(component(this@MediaListener)))
    }

    // Moves the progress bar along while something is playing.
    private val ticker = object : Runnable {
        override fun run() {
            render(progressOnly = true)
            if (track.value?.playing == true) handler.postDelayed(this, 1000)
        }
    }

    override fun onListenerConnected() {
        sessions = getSystemService(MediaSessionManager::class.java)
        sessions.addOnActiveSessionsChangedListener(sessionsChanged, component(this), handler)
        pick(sessions.getActiveSessions(component(this)))
    }

    override fun onListenerDisconnected() {
        if (::sessions.isInitialized) sessions.removeOnActiveSessionsChangedListener(sessionsChanged)
        controller?.unregisterCallback(callback)
        controller = null
        handler.removeCallbacks(ticker)
        track.value = null
        NowPlayingWidget.updateAll(this, null)
    }

    /** Prefer whichever session is actually playing; otherwise the most recent one. */
    private fun pick(list: List<MediaController>) {
        val chosen = list.firstOrNull { it.playbackState?.state == PlaybackState.STATE_PLAYING } ?: list.firstOrNull()
        if (chosen?.sessionToken != controller?.sessionToken) {
            controller?.unregisterCallback(callback)
            controller = chosen
            chosen?.registerCallback(callback, handler)
        }
        render()
    }

    private fun render(progressOnly: Boolean = false) {
        val c = controller
        val t = c?.let { toTrack(it) }
        track.value = t
        val key = t?.let { "${it.appPackage}|${it.title}|${it.artist}|${it.playing}|${it.art?.generationId}" }
        if (progressOnly && key == lastKey) NowPlayingWidget.updateProgress(this, t)
        else NowPlayingWidget.updateAll(this, t)
        lastKey = key
        handler.removeCallbacks(ticker)
        if (t?.playing == true) handler.postDelayed(ticker, 1000)
    }

    private fun toTrack(c: MediaController): Track? {
        val md = c.metadata ?: return null
        val state = c.playbackState
        val title = md.getString(MediaMetadata.METADATA_KEY_TITLE)
            ?: md.getString(MediaMetadata.METADATA_KEY_DISPLAY_TITLE) ?: return null
        val artist = md.getString(MediaMetadata.METADATA_KEY_ARTIST)
            ?: md.getString(MediaMetadata.METADATA_KEY_ALBUM_ARTIST)
            ?: md.getString(MediaMetadata.METADATA_KEY_DISPLAY_SUBTITLE) ?: ""
        val playing = state?.state == PlaybackState.STATE_PLAYING
        val duration = md.getLong(MediaMetadata.METADATA_KEY_DURATION).coerceAtLeast(0)
        var position = state?.position ?: 0L
        if (playing && state != null && state.lastPositionUpdateTime > 0) {
            position += ((SystemClock.elapsedRealtime() - state.lastPositionUpdateTime) * state.playbackSpeed).toLong()
        }
        if (duration > 0) position = position.coerceIn(0, duration)
        return Track(title, artist, art(md), duration, position, playing, c.packageName, c.sessionActivity)
    }

    /** Cover art, scaled down once per image so widget updates stay small. */
    private fun art(md: MediaMetadata): Bitmap? {
        val src = md.getBitmap(MediaMetadata.METADATA_KEY_ART)
            ?: md.getBitmap(MediaMetadata.METADATA_KEY_ALBUM_ART)
            ?: md.getBitmap(MediaMetadata.METADATA_KEY_DISPLAY_ICON) ?: return null
        if (src === artSource) return artScaled
        val max = 480f
        val scale = minOf(1f, max / maxOf(src.width, src.height))
        artSource = src
        artScaled = if (scale < 1f)
            Bitmap.createScaledBitmap(src, (src.width * scale).toInt().coerceAtLeast(1), (src.height * scale).toInt().coerceAtLeast(1), true)
        else src
        return artScaled
    }
}
