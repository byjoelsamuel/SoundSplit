package com.soundsplit.app

import android.Manifest
import android.annotation.SuppressLint
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.database.ContentObserver
import android.media.AudioAttributes
import android.media.AudioDeviceInfo
import android.media.AudioFormat
import android.media.AudioManager
import android.media.AudioPlaybackCaptureConfiguration
import android.media.AudioRecord
import android.media.AudioTrack
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.provider.Settings
import androidx.core.content.IntentCompat
import kotlinx.coroutines.flow.MutableStateFlow
import kotlin.math.roundToInt

/**
 * Android has no public API to move another app's audio to a different device. What it does allow
 * (Android 10+) is *capturing* other apps' audio, the same permission screen recorders use. So:
 *
 *  1. Selected apps are captured and played on the chosen device.
 *  2. With "Silence original" on, media is muted and everything *else* is captured too and played
 *     on the normal output, so each sound only comes out of one place. Our own playback uses the
 *     alarm stream so it isn't silenced by that mute.
 */
class RouteService : Service() {

    companion object {
        val running = MutableStateFlow(false)
        val error = MutableStateFlow<String?>(null)

        const val EXTRA_CODE = "code"
        const val EXTRA_DATA = "data"
        private const val ACTION_STOP = "com.soundsplit.app.STOP"
        private const val CHANNEL = "routing"
        private const val NOTIFICATION_ID = 1

        @Volatile private var instance: RouteService? = null

        fun stop(context: Context) {
            context.startService(Intent(context, RouteService::class.java).setAction(ACTION_STOP))
        }

        fun setGain(gain: Float) {
            instance?.pipes?.forEach { it.setGain(gain) }
        }

        /** If the app was killed while media was muted, put things back on next launch. */
        fun restoreIfNeeded(context: Context) {
            if (running.value) return
            val prefs = Prefs(context)
            if (!prefs.mutedMedia) return
            val am = context.getSystemService(AudioManager::class.java)
            restoreVolumes(am, prefs)
        }

        private fun restoreVolumes(am: AudioManager, prefs: Prefs) {
            try { am.adjustStreamVolume(AudioManager.STREAM_MUSIC, AudioManager.ADJUST_UNMUTE, 0) } catch (_: Exception) {}
            val alarm = prefs.savedAlarmVolume
            if (alarm >= 0) try { am.setStreamVolume(AudioManager.STREAM_ALARM, alarm, 0) } catch (_: Exception) {}
            prefs.savedAlarmVolume = -1
            prefs.mutedMedia = false
        }
    }

    private lateinit var am: AudioManager
    private lateinit var prefs: Prefs
    private var projection: MediaProjection? = null
    private val pipes = mutableListOf<Pipe>()
    private var muteGuard: ContentObserver? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        am = getSystemService(AudioManager::class.java)
        prefs = Prefs(this)
        instance = this
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            shutdown()
            stopSelf()
            return START_NOT_STICKY
        }
        // Must be in the foreground (with the mediaProjection type) before using the projection.
        startForeground(NOTIFICATION_ID, notification("Starting…"), ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION)
        try {
            val code = intent?.getIntExtra(EXTRA_CODE, 0) ?: 0
            val data = intent?.let { IntentCompat.getParcelableExtra(it, EXTRA_DATA, Intent::class.java) }
                ?: error("Missing permission result")
            begin(code, data)
        } catch (e: Exception) {
            error.value = e.message ?: e.toString()
            shutdown()
            stopSelf()
        }
        return START_NOT_STICKY
    }

    private fun begin(code: Int, data: Intent) {
        error.value = null
        if (checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED)
            error("Audio permission is off. Allow it in Settings → Apps → SoundSplit.")
        val target = Outputs.find(am, prefs.deviceId) ?: error("Pick an output device first.")
        val uids = prefs.packages.mapNotNull { Outputs.uidOf(packageManager, it) }.distinct()
        if (uids.isEmpty()) error("Pick at least one app first.")

        val mpm = getSystemService(MediaProjectionManager::class.java)
        val mp = mpm.getMediaProjection(code, data) ?: error("Capture permission was refused.")
        projection = mp
        mp.registerCallback(object : MediaProjection.Callback() {
            override fun onStop() { shutdown(); stopSelf() }
        }, Handler(Looper.getMainLooper()))

        val silence = prefs.silence
        val mainOutput = Outputs.defaultMedia(am)   // read before anything changes
        val usage = if (silence) AudioAttributes.USAGE_ALARM else AudioAttributes.USAGE_MEDIA

        // 1) Selected apps -> chosen device
        pipes += Pipe(capture(mp) { uids.forEach(::addMatchingUid) }, target, usage)

        // 2) Everything else -> normal output, then mute the originals
        if (silence) {
            pipes += Pipe(capture(mp) { uids.forEach(::excludeUid) }, mainOutput, usage)
            muteMedia()
        }

        pipes.forEach { it.setGain(prefs.gain); it.start() }
        running.value = true

        val names = prefs.packages.size
        val nm = getSystemService(NotificationManager::class.java)
        nm.notify(NOTIFICATION_ID, notification("$names app${if (names == 1) "" else "s"} → ${Outputs.list(am).firstOrNull { it.id == target.id }?.name ?: "device"}"))
    }

    private fun capture(mp: MediaProjection, filter: AudioPlaybackCaptureConfiguration.Builder.() -> Unit) =
        AudioPlaybackCaptureConfiguration.Builder(mp)
            .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
            .addMatchingUsage(AudioAttributes.USAGE_GAME)
            .addMatchingUsage(AudioAttributes.USAGE_UNKNOWN)
            .apply(filter)
            .build()

    private fun muteMedia() {
        try {
            // Our playback rides the alarm stream; start it at the same loudness media had.
            prefs.savedAlarmVolume = am.getStreamVolume(AudioManager.STREAM_ALARM)
            val musicFraction = am.getStreamVolume(AudioManager.STREAM_MUSIC).toFloat() /
                am.getStreamMaxVolume(AudioManager.STREAM_MUSIC).coerceAtLeast(1)
            val alarmMax = am.getStreamMaxVolume(AudioManager.STREAM_ALARM)
            am.setStreamVolume(AudioManager.STREAM_ALARM, (musicFraction * alarmMax).roundToInt().coerceIn(1, alarmMax), 0)
        } catch (_: Exception) { /* DND can block this; not fatal */ }

        prefs.mutedMedia = true
        am.adjustStreamVolume(AudioManager.STREAM_MUSIC, AudioManager.ADJUST_MUTE, 0)

        // Volume keys unmute media; keep it muted while we're routing.
        muteGuard = object : ContentObserver(Handler(Looper.getMainLooper())) {
            override fun onChange(selfChange: Boolean) {
                if (running.value && !am.isStreamMute(AudioManager.STREAM_MUSIC))
                    am.adjustStreamVolume(AudioManager.STREAM_MUSIC, AudioManager.ADJUST_MUTE, 0)
            }
        }.also { contentResolver.registerContentObserver(Settings.System.CONTENT_URI, true, it) }
    }

    private fun shutdown() {
        pipes.forEach { it.stop() }
        pipes.clear()
        muteGuard?.let { contentResolver.unregisterContentObserver(it) }
        muteGuard = null
        if (prefs.mutedMedia) restoreVolumes(am, prefs)
        try { projection?.stop() } catch (_: Exception) {}
        projection = null
        running.value = false
        stopForeground(STOP_FOREGROUND_REMOVE)
    }

    override fun onDestroy() {
        shutdown()
        instance = null
        super.onDestroy()
    }

    private fun notification(text: String): Notification {
        val nm = getSystemService(NotificationManager::class.java)
        nm.createNotificationChannel(NotificationChannel(CHANNEL, "Routing", NotificationManager.IMPORTANCE_LOW))
        val open = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE)
        val stop = PendingIntent.getService(this, 1, Intent(this, RouteService::class.java).setAction(ACTION_STOP), PendingIntent.FLAG_IMMUTABLE)
        return Notification.Builder(this, CHANNEL)
            .setSmallIcon(R.drawable.ic_stat)
            .setContentTitle("SoundSplit")
            .setContentText(text)
            .setContentIntent(open)
            .setOngoing(true)
            .addAction(Notification.Action.Builder(null, "Stop", stop).build())
            .build()
    }

    /** Copies one captured stream to one output device on its own thread. */
    @SuppressLint("MissingPermission") // RECORD_AUDIO is checked in begin() before any Pipe is created
    private class Pipe(config: AudioPlaybackCaptureConfiguration, device: AudioDeviceInfo?, usage: Int) {
        private val rate = 48_000
        private val record: AudioRecord
        private val track: AudioTrack
        @Volatile private var active = false
        private var thread: Thread? = null

        init {
            val inFormat = AudioFormat.Builder().setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(rate).setChannelMask(AudioFormat.CHANNEL_IN_STEREO).build()
            val outFormat = AudioFormat.Builder().setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(rate).setChannelMask(AudioFormat.CHANNEL_OUT_STEREO).build()
            val inBuf = AudioRecord.getMinBufferSize(rate, AudioFormat.CHANNEL_IN_STEREO, AudioFormat.ENCODING_PCM_16BIT)
            val outBuf = AudioTrack.getMinBufferSize(rate, AudioFormat.CHANNEL_OUT_STEREO, AudioFormat.ENCODING_PCM_16BIT)

            record = AudioRecord.Builder()
                .setAudioFormat(inFormat)
                .setBufferSizeInBytes(inBuf * 2)
                .setAudioPlaybackCaptureConfig(config)
                .build()
            track = AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(usage)
                        .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                        .setAllowedCapturePolicy(AudioAttributes.ALLOW_CAPTURE_BY_NONE)
                        .build())
                .setAudioFormat(outFormat)
                .setBufferSizeInBytes(outBuf * 2)
                .setTransferMode(AudioTrack.MODE_STREAM)
                .setPerformanceMode(AudioTrack.PERFORMANCE_MODE_LOW_LATENCY)
                .build()
            device?.let { track.preferredDevice = it }
        }

        fun setGain(gain: Float) { track.setVolume(gain.coerceIn(0f, 1f)) }

        fun start() {
            active = true
            record.startRecording()
            track.play()
            thread = Thread {
                val buf = ShortArray(960)
                while (active) {
                    val n = record.read(buf, 0, buf.size)
                    if (n > 0) track.write(buf, 0, n)
                }
            }.apply { name = "soundsplit-pipe"; priority = Thread.MAX_PRIORITY; start() }
        }

        fun stop() {
            active = false
            try { record.stop() } catch (_: Exception) {}
            thread?.join(500)
            try { track.stop() } catch (_: Exception) {}
            record.release()
            track.release()
        }
    }
}
