package com.soundsplit.app

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.media.AudioAttributes
import android.media.AudioDeviceInfo
import android.media.AudioManager
import android.os.Build
import androidx.core.graphics.drawable.toBitmap

enum class Kind { Speaker, Wired, Bluetooth, Usb, Hdmi, Other }

data class OutputDevice(val id: Int, val name: String, val kind: Kind)

data class AppEntry(val pkg: String, val label: String, val icon: Bitmap?)

object Outputs {
    private val kinds = mapOf(
        AudioDeviceInfo.TYPE_BUILTIN_SPEAKER to Kind.Speaker,
        AudioDeviceInfo.TYPE_WIRED_HEADPHONES to Kind.Wired,
        AudioDeviceInfo.TYPE_WIRED_HEADSET to Kind.Wired,
        AudioDeviceInfo.TYPE_BLUETOOTH_A2DP to Kind.Bluetooth,
        AudioDeviceInfo.TYPE_BLE_HEADSET to Kind.Bluetooth,
        AudioDeviceInfo.TYPE_BLE_SPEAKER to Kind.Bluetooth,
        AudioDeviceInfo.TYPE_USB_HEADSET to Kind.Usb,
        AudioDeviceInfo.TYPE_USB_DEVICE to Kind.Usb,
        AudioDeviceInfo.TYPE_USB_ACCESSORY to Kind.Usb,
        AudioDeviceInfo.TYPE_HDMI to Kind.Hdmi,
        AudioDeviceInfo.TYPE_LINE_ANALOG to Kind.Wired,
        AudioDeviceInfo.TYPE_LINE_DIGITAL to Kind.Wired,
        AudioDeviceInfo.TYPE_AUX_LINE to Kind.Wired,
        AudioDeviceInfo.TYPE_DOCK to Kind.Other,
    )

    /** Speakers, headphones, DACs and Bluetooth outputs that are connected right now. */
    fun list(am: AudioManager): List<OutputDevice> =
        am.getDevices(AudioManager.GET_DEVICES_OUTPUTS)
            .filter { it.type in kinds }
            .map { OutputDevice(it.id, nameOf(it), kinds.getValue(it.type)) }
            .distinctBy { it.name }

    fun find(am: AudioManager, id: Int): AudioDeviceInfo? =
        am.getDevices(AudioManager.GET_DEVICES_OUTPUTS).firstOrNull { it.id == id }

    /** Where media plays when nothing is routed (used to send "everything else" back there). */
    fun defaultMedia(am: AudioManager): AudioDeviceInfo? {
        if (Build.VERSION.SDK_INT >= 33) {
            val attrs = AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_MEDIA).build()
            am.getAudioDevicesForAttributes(attrs).firstOrNull()?.let { info ->
                find(am, info.id)?.let { return it }
            }
        }
        val priority = listOf(
            AudioDeviceInfo.TYPE_BLUETOOTH_A2DP, AudioDeviceInfo.TYPE_BLE_HEADSET,
            AudioDeviceInfo.TYPE_USB_HEADSET, AudioDeviceInfo.TYPE_USB_DEVICE,
            AudioDeviceInfo.TYPE_WIRED_HEADPHONES, AudioDeviceInfo.TYPE_WIRED_HEADSET,
            AudioDeviceInfo.TYPE_BUILTIN_SPEAKER,
        )
        val outs = am.getDevices(AudioManager.GET_DEVICES_OUTPUTS)
        return priority.firstNotNullOfOrNull { t -> outs.firstOrNull { it.type == t } }
    }

    private fun nameOf(d: AudioDeviceInfo): String = when (d.type) {
        AudioDeviceInfo.TYPE_BUILTIN_SPEAKER -> "Phone speaker"
        AudioDeviceInfo.TYPE_WIRED_HEADPHONES, AudioDeviceInfo.TYPE_WIRED_HEADSET -> "Wired headphones"
        else -> d.productName?.toString()?.takeIf { it.isNotBlank() && it != Build.MODEL } ?: when (kinds[d.type]) {
            Kind.Bluetooth -> "Bluetooth"
            Kind.Usb -> "USB audio"
            Kind.Hdmi -> "HDMI"
            else -> "Audio output"
        }
    }

    /** Apps with a launcher icon, alphabetical. */
    fun apps(context: Context): List<AppEntry> {
        val pm = context.packageManager
        val intent = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_LAUNCHER)
        @Suppress("DEPRECATION")
        val activities = pm.queryIntentActivities(intent, 0)
        return activities.asSequence()
            .map { it.activityInfo.applicationInfo }
            .filter { it.packageName != context.packageName }
            .distinctBy { it.packageName }
            .map { ai ->
                val icon = try { pm.getApplicationIcon(ai).toBitmap(96, 96) } catch (_: Exception) { null }
                AppEntry(ai.packageName, pm.getApplicationLabel(ai).toString(), icon)
            }
            .sortedBy { it.label.lowercase() }
            .toList()
    }

    fun uidOf(pm: PackageManager, pkg: String): Int? =
        try { pm.getApplicationInfo(pkg, 0).uid } catch (_: PackageManager.NameNotFoundException) { null }
}
