package com.soundsplit.app

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.service.notification.NotificationListenerService
import android.view.View
import android.widget.RemoteViews

/** 3×3 home-screen widget: cover art, title, artist, progress and playback controls. */
class NowPlayingWidget : AppWidgetProvider() {

    companion object {
        private const val ACTION_PLAY_PAUSE = "com.soundsplit.app.widget.PLAY_PAUSE"
        private const val ACTION_NEXT = "com.soundsplit.app.widget.NEXT"
        private const val ACTION_PREV = "com.soundsplit.app.widget.PREV"

        private fun ids(context: Context): IntArray =
            AppWidgetManager.getInstance(context).getAppWidgetIds(ComponentName(context, NowPlayingWidget::class.java))

        fun updateAll(context: Context, track: Track?) {
            val ids = ids(context)
            if (ids.isEmpty()) return
            AppWidgetManager.getInstance(context).updateAppWidget(ids, build(context, track))
        }

        /** Cheap update while playing: only the progress bar and times. */
        fun updateProgress(context: Context, track: Track?) {
            val ids = ids(context)
            if (ids.isEmpty() || track == null) return
            val rv = RemoteViews(context.packageName, R.layout.widget_now_playing)
            applyProgress(rv, track)
            AppWidgetManager.getInstance(context).partiallyUpdateAppWidget(ids, rv)
        }

        private fun build(context: Context, track: Track?): RemoteViews {
            val rv = RemoteViews(context.packageName, R.layout.widget_now_playing)
            val setup = !MediaListener.isEnabled(context)

            if (track == null) {
                rv.setImageViewResource(R.id.art, R.drawable.widget_art_placeholder)
                rv.setTextViewText(R.id.title, if (setup) "Set up widget" else "Nothing playing")
                rv.setTextViewText(R.id.artist, if (setup) "Tap to allow media access" else "Play something in any app")
                rv.setProgressBar(R.id.progress, 1000, 0, false)
                rv.setTextViewText(R.id.elapsed, "")
                rv.setTextViewText(R.id.duration, "")
                rv.setImageViewResource(R.id.play, R.drawable.ic_widget_play)
            } else {
                if (track.art != null) rv.setImageViewBitmap(R.id.art, track.art)
                else rv.setImageViewResource(R.id.art, R.drawable.widget_art_placeholder)
                rv.setTextViewText(R.id.title, track.title)
                rv.setTextViewText(R.id.artist, track.artist)
                rv.setImageViewResource(R.id.play, if (track.playing) R.drawable.ic_widget_pause else R.drawable.ic_widget_play)
                rv.setContentDescription(R.id.play, if (track.playing) "Pause" else "Play")
                applyProgress(rv, track)
            }

            val openSoundSplit = PendingIntent.getActivity(
                context, 10,
                Intent(context, MainActivity::class.java).putExtra(MainActivity.EXTRA_TAB, MainActivity.TAB_WIDGET)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP),
                PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
            // Tapping the cover opens the playing app (e.g. the Brave tab); otherwise SoundSplit.
            rv.setOnClickPendingIntent(R.id.art, if (setup) openSoundSplit else track?.openApp ?: openSoundSplit)
            rv.setOnClickPendingIntent(R.id.texts, if (setup) openSoundSplit else track?.openApp ?: openSoundSplit)
            rv.setOnClickPendingIntent(R.id.play, action(context, ACTION_PLAY_PAUSE, 1))
            rv.setOnClickPendingIntent(R.id.next, action(context, ACTION_NEXT, 2))
            rv.setOnClickPendingIntent(R.id.prev, action(context, ACTION_PREV, 3))
            val controls = if (track == null) View.INVISIBLE else View.VISIBLE
            rv.setViewVisibility(R.id.next, controls)
            rv.setViewVisibility(R.id.prev, controls)
            return rv
        }

        private fun applyProgress(rv: RemoteViews, t: Track) {
            val hasDuration = t.durationMs > 0
            rv.setProgressBar(R.id.progress, 1000, if (hasDuration) (t.positionMs * 1000 / t.durationMs).toInt() else 0, false)
            rv.setTextViewText(R.id.elapsed, if (hasDuration) TimeFormat.mmss(t.positionMs) else "")
            rv.setTextViewText(R.id.duration, if (hasDuration) TimeFormat.mmss(t.durationMs) else "")
        }

        private fun action(context: Context, action: String, code: Int) = PendingIntent.getBroadcast(
            context, code, Intent(context, NowPlayingWidget::class.java).setAction(action),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
    }

    override fun onUpdate(context: Context, manager: AppWidgetManager, appWidgetIds: IntArray) {
        // Make sure the media listener is running so the widget fills in straight away.
        if (MediaListener.isEnabled(context)) {
            try { NotificationListenerService.requestRebind(MediaListener.component(context)) } catch (_: Exception) {}
        }
        updateAll(context, MediaListener.track.value)
    }

    override fun onReceive(context: Context, intent: Intent) {
        val controls = MediaListener.controller?.transportControls
        when (intent.action) {
            ACTION_PLAY_PAUSE -> if (MediaListener.track.value?.playing == true) controls?.pause() else controls?.play()
            ACTION_NEXT -> controls?.skipToNext()
            ACTION_PREV -> controls?.skipToPrevious()
            else -> return super.onReceive(context, intent)
        }
        if (controls == null) {
            context.startActivity(Intent(context, MainActivity::class.java)
                .putExtra(MainActivity.EXTRA_TAB, MainActivity.TAB_WIDGET)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        }
    }
}

object TimeFormat {
    /** 65_000 -> "1:05", 3_725_000 -> "1:02:05". */
    fun mmss(ms: Long): String {
        val total = (ms.coerceAtLeast(0) / 1000)
        val h = total / 3600; val m = (total % 3600) / 60; val s = total % 60
        return if (h > 0) "%d:%02d:%02d".format(h, m, s) else "%d:%02d".format(m, s)
    }
}
