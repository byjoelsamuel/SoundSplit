package com.soundsplit.app

import android.annotation.SuppressLint
import android.content.Context

/** Small key/value store for the user's choices. */
@SuppressLint("ApplySharedPref") // commit() on purpose: the mute flags must survive a crash
class Prefs(context: Context) {
    private val sp = context.getSharedPreferences("soundsplit", Context.MODE_PRIVATE)

    var packages: Set<String>
        get() = sp.getStringSet("packages", emptySet())!!.toSet()
        set(v) = sp.edit().putStringSet("packages", v).apply()

    var deviceId: Int
        get() = sp.getInt("device", -1)
        set(v) = sp.edit().putInt("device", v).apply()

    var silence: Boolean
        get() = sp.getBoolean("silence", true)
        set(v) = sp.edit().putBoolean("silence", v).apply()

    var gain: Float
        get() = sp.getFloat("gain", 1f)
        set(v) = sp.edit().putFloat("gain", v).apply()

    // Crash safety: remember that we muted media so it can be restored on next launch.
    var mutedMedia: Boolean
        get() = sp.getBoolean("muted", false)
        set(v) { sp.edit().putBoolean("muted", v).commit() }

    var savedAlarmVolume: Int
        get() = sp.getInt("alarm", -1)
        set(v) { sp.edit().putInt("alarm", v).commit() }
}
