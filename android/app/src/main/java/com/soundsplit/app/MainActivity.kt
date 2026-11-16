package com.soundsplit.app

import android.Manifest
import android.app.Activity
import android.appwidget.AppWidgetManager
import android.content.ComponentName
import android.net.Uri
import android.provider.Settings
import android.content.Intent
import android.graphics.Bitmap
import android.media.AudioDeviceCallback
import android.media.AudioDeviceInfo
import android.media.AudioManager
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.TextButton
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.Typography
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext

private val Bg = Color(0xFFF4F5F7)
private val Card = Color.White
private val Line = Color(0xFFE4E7EC)
private val Ink = Color(0xFF101828)
private val Muted = Color(0xFF667085)
private val Accent = Color(0xFF4F46E5)
private val AccentSoft = Color(0xFFEEF0FF)
private val Danger = Color(0xFFD92D20)

private val Nunito = FontFamily(
    Font(R.font.nunito_regular, FontWeight.Normal),
    Font(R.font.nunito_semibold, FontWeight.SemiBold),
    Font(R.font.nunito_bold, FontWeight.Bold),
)

class MainActivity : ComponentActivity() {

    companion object {
        const val EXTRA_TAB = "tab"
        const val TAB_SPLIT = 0
        const val TAB_WIDGET = 1
    }

    private val tab = mutableStateOf(TAB_SPLIT)

    private val captureConsent = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) { r ->
        val data = r.data
        if (r.resultCode == Activity.RESULT_OK && data != null) {
            ContextCompat.startForegroundService(this, Intent(this, RouteService::class.java)
                .putExtra(RouteService.EXTRA_CODE, r.resultCode)
                .putExtra(RouteService.EXTRA_DATA, data))
        }
    }

    private val permissions = registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { granted ->
        if (granted[Manifest.permission.RECORD_AUDIO] == true) {
            val mpm = getSystemService(MediaProjectionManager::class.java)
            captureConsent.launch(mpm.createScreenCaptureIntent())
        } else {
            Toast.makeText(this, "SoundSplit needs the audio permission to copy app sound.", Toast.LENGTH_LONG).show()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        RouteService.restoreIfNeeded(this)
        tab.value = intent.getIntExtra(EXTRA_TAB, TAB_SPLIT)
        setContent {
            MaterialTheme(
                colorScheme = lightColorScheme(primary = Accent, background = Bg, surface = Card, onSurface = Ink),
                typography = Typography(bodyLarge = TextStyle(fontFamily = Nunito, fontSize = 15.sp)),
            ) { App(tab.value, onTab = { tab.value = it }, onStart = ::start, onStop = { RouteService.stop(this) }) }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        if (intent.hasExtra(EXTRA_TAB)) tab.value = intent.getIntExtra(EXTRA_TAB, TAB_SPLIT)
    }

    private fun start() {
        val perms = mutableListOf(Manifest.permission.RECORD_AUDIO)
        if (Build.VERSION.SDK_INT >= 33) perms += Manifest.permission.POST_NOTIFICATIONS
        permissions.launch(perms.toTypedArray())
    }
}

@Composable
private fun App(tab: Int, onTab: (Int) -> Unit, onStart: () -> Unit, onStop: () -> Unit) {
    val context = LocalContext.current
    var release by remember { mutableStateOf<Updater.Release?>(null) }
    var showUpdate by remember { mutableStateOf(false) }
    var downloadId by remember { mutableLongStateOf(-1L) }
    var download by remember { mutableStateOf<Updater.Download?>(null) }

    // Check GitHub once per launch; pop up if a newer version exists.
    LaunchedEffect(Unit) {
        release = Updater.check(BuildConfig.VERSION_NAME)
        if (release != null) showUpdate = true
    }
    LaunchedEffect(downloadId) {
        if (downloadId < 0) return@LaunchedEffect
        while (true) {
            val d = Updater.progress(context, downloadId)
            download = d
            if (d is Updater.Download.Done) { Updater.install(context, downloadId); break }
            if (d is Updater.Download.Failed) break
            delay(400)
        }
    }

    Column(Modifier.fillMaxSize().background(Bg).statusBarsPadding().navigationBarsPadding()) {
        Row(Modifier.padding(start = 20.dp, end = 20.dp, top = 20.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(
                Modifier.size(38.dp).clip(RoundedCornerShape(11.dp)).background(Color.White)
                    .border(BorderStroke(2.dp, Ink), RoundedCornerShape(11.dp)),
                contentAlignment = Alignment.Center,
            ) { Image(painterResource(R.drawable.ic_glyph), null, Modifier.size(30.dp)) }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Label("SoundSplit", 20.sp, FontWeight.Bold)
                Label(if (tab == MainActivity.TAB_WIDGET) "Now-playing widget" else "Play chosen apps on another output", 13.sp, color = Muted)
            }
            if (release != null && !showUpdate) {
                Box(
                    Modifier.clip(CircleShape).background(AccentSoft).clickable { showUpdate = true }
                        .padding(horizontal = 12.dp, vertical = 6.dp),
                ) { Label("Update", 12.sp, FontWeight.Bold, Accent) }
            }
        }

        // Tabs
        Row(
            Modifier.padding(20.dp).fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(Line).padding(3.dp),
        ) {
            listOf("Split" to MainActivity.TAB_SPLIT, "Widget" to MainActivity.TAB_WIDGET).forEach { (name, id) ->
                val on = tab == id
                Box(
                    Modifier.weight(1f).clip(RoundedCornerShape(10.dp)).background(if (on) Card else Line)
                        .clickable { onTab(id) }.padding(vertical = 9.dp),
                    contentAlignment = Alignment.Center,
                ) { Label(name, 14.sp, FontWeight.Bold, if (on) Ink else Muted) }
            }
        }

        if (tab == MainActivity.TAB_WIDGET) WidgetScreen() else Screen(onStart, onStop)
    }

    val r = release
    if (showUpdate && r != null) {
        val d = download
        AlertDialog(
            onDismissRequest = { if (d !is Updater.Download.Running) showUpdate = false },
            title = { Label("Update available", 18.sp, FontWeight.Bold) },
            text = {
                Column {
                    Label("SoundSplit ${r.version} is ready. You have ${BuildConfig.VERSION_NAME}.", 14.sp)
                    when (d) {
                        is Updater.Download.Running -> {
                            Spacer(Modifier.height(14.dp))
                            LinearProgressIndicator(progress = { d.fraction }, modifier = Modifier.fillMaxWidth(), color = Accent)
                            Spacer(Modifier.height(6.dp))
                            Label("Downloading… ${(d.fraction * 100).toInt()}%", 12.sp, color = Muted)
                        }
                        Updater.Download.Done -> {
                            Spacer(Modifier.height(10.dp))
                            Label("Downloaded. If Android asks, allow SoundSplit to install apps, then press Install again.", 12.sp, color = Muted)
                        }
                        Updater.Download.Failed -> {
                            Spacer(Modifier.height(10.dp))
                            Label("Download failed. Check your connection and try again.", 12.sp, color = Danger)
                        }
                        null -> {}
                    }
                }
            },
            confirmButton = {
                TextButton(
                    enabled = d !is Updater.Download.Running,
                    onClick = {
                        if (d == Updater.Download.Done) Updater.install(context, downloadId)
                        else { download = Updater.Download.Running(0f); downloadId = Updater.startDownload(context, r) }
                    },
                ) { Label(if (d == Updater.Download.Done) "Install" else "Update", 14.sp, FontWeight.Bold, Accent) }
            },
            dismissButton = {
                if (d !is Updater.Download.Running)
                    TextButton(onClick = { showUpdate = false }) { Label("Later", 14.sp, FontWeight.SemiBold, Muted) }
            },
            containerColor = Card,
        )
    }
}

@Composable
private fun WidgetScreen() {
    val context = LocalContext.current
    val track by MediaListener.track.collectAsState()
    var enabled by remember { mutableStateOf(MediaListener.isEnabled(context)) }
    // Re-check when the user comes back from the settings screen.
    LaunchedEffect(Unit) { while (true) { enabled = MediaListener.isEnabled(context); delay(1000) } }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 20.dp, end = 20.dp, bottom = 20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        item {
            Panel {
                Label("Now-playing widget", 16.sp, FontWeight.Bold)
                Label("A 3×3 home-screen widget with cover art and controls for whatever is playing: YouTube Music in Brave, Spotify, YouTube Music, podcasts, anything.", 13.sp, color = Muted)
            }
        }

        item {
            Panel {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Label("1. Allow media access", 14.sp, FontWeight.Bold)
                        Label(if (enabled) "On" else "Needed to see what's playing in other apps. SoundSplit only reads the song info; it doesn't read your notifications.", 12.sp, color = if (enabled) Accent else Muted)
                    }
                    if (!enabled) {
                        Spacer(Modifier.width(10.dp))
                        SmallButton("Allow") { openListenerSettings(context) }
                    }
                }
                if (!enabled) {
                    Spacer(Modifier.height(8.dp))
                    Label("Android says \"Restricted setting\"? Open App info → ⋮ (top right) → Allow restricted settings, then tap Allow again.", 12.sp, color = Muted)
                    Spacer(Modifier.height(6.dp))
                    SmallButton("App info") {
                        context.startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:${context.packageName}")))
                    }
                }
            }
        }

        item {
            Panel {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Label("2. Add it to your home screen", 14.sp, FontWeight.Bold)
                        Label("Or long-press your home screen → Widgets → SoundSplit.", 12.sp, color = Muted)
                    }
                    Spacer(Modifier.width(10.dp))
                    SmallButton("Add") {
                        val mgr = AppWidgetManager.getInstance(context)
                        val ok = mgr.isRequestPinAppWidgetSupported &&
                            mgr.requestPinAppWidget(ComponentName(context, NowPlayingWidget::class.java), null, null)
                        if (!ok) Toast.makeText(context, "Long-press your home screen → Widgets → SoundSplit", Toast.LENGTH_LONG).show()
                    }
                }
            }
        }

        item {
            Label("Preview", 14.sp, FontWeight.Bold, modifier = Modifier.padding(top = 6.dp))
        }
        item { WidgetPreview(track, enabled) }
    }
}

/** The same card as the home-screen widget, live and tappable. */
@Composable
private fun WidgetPreview(track: Track?, enabled: Boolean) {
    val controls = MediaListener.controller?.transportControls
    Column(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(28.dp))
            .background(androidx.compose.ui.graphics.Brush.verticalGradient(listOf(Color(0xE6262A4A), Color(0xF2151726))))
            .padding(14.dp),
    ) {
        Box(
            Modifier.fillMaxWidth().height(180.dp).clip(RoundedCornerShape(18.dp))
                .background(androidx.compose.ui.graphics.Brush.linearGradient(listOf(Color(0xFF4F46E5), Color(0xFF06B6D4)))),
            contentAlignment = Alignment.Center,
        ) {
            val art = track?.art
            if (art != null) Image(art.asImageBitmap(), null, Modifier.fillMaxSize(), contentScale = androidx.compose.ui.layout.ContentScale.Crop)
            else Image(painterResource(R.drawable.ic_music_note), null, Modifier.size(48.dp))
        }
        Spacer(Modifier.height(10.dp))
        Label(track?.title ?: if (enabled) "Nothing playing" else "Set up widget", 15.sp, FontWeight.Bold, Color.White)
        Label(track?.artist ?: if (enabled) "Play something in any app" else "Allow media access above", 12.sp, color = Color(0xB3FFFFFF))
        Spacer(Modifier.height(8.dp))
        val frac = if (track != null && track.durationMs > 0) track.positionMs.toFloat() / track.durationMs else 0f
        LinearProgressIndicator(progress = { frac }, modifier = Modifier.fillMaxWidth().height(4.dp).clip(CircleShape),
            color = Color.White, trackColor = Color(0x40FFFFFF), drawStopIndicator = {})
        Row(Modifier.fillMaxWidth().padding(top = 3.dp)) {
            Label(if (track != null && track.durationMs > 0) TimeFormat.mmss(track.positionMs) else "", 10.sp, color = Color(0x99FFFFFF), modifier = Modifier.weight(1f))
            Label(if (track != null && track.durationMs > 0) TimeFormat.mmss(track.durationMs) else "", 10.sp, color = Color(0x99FFFFFF))
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            Control(R.drawable.ic_widget_prev, false, track != null) { controls?.skipToPrevious() }
            Spacer(Modifier.width(18.dp))
            Control(if (track?.playing == true) R.drawable.ic_widget_pause else R.drawable.ic_widget_play, true, track != null) {
                if (track?.playing == true) controls?.pause() else controls?.play()
            }
            Spacer(Modifier.width(18.dp))
            Control(R.drawable.ic_widget_next, false, track != null) { controls?.skipToNext() }
        }
    }
}

@Composable
private fun Control(icon: Int, primary: Boolean, enabled: Boolean, onClick: () -> Unit) {
    Box(
        Modifier.size(if (primary) 44.dp else 40.dp).clip(CircleShape)
            .background(if (primary) Color.White else Color(0x1FFFFFFF))
            .clickable(enabled = enabled, onClick = onClick)
            .alpha(if (enabled || primary) 1f else 0.4f),
        contentAlignment = Alignment.Center,
    ) { Image(painterResource(icon), null, Modifier.size(if (primary) 22.dp else 20.dp)) }
}

@Composable
private fun SmallButton(text: String, onClick: () -> Unit) {
    Box(
        Modifier.clip(RoundedCornerShape(10.dp)).background(Accent).clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 8.dp),
    ) { Label(text, 13.sp, FontWeight.Bold, Color.White) }
}

private fun openListenerSettings(context: android.content.Context) {
    val component = MediaListener.component(context).flattenToString()
    val detail = Intent(Settings.ACTION_NOTIFICATION_LISTENER_DETAIL_SETTINGS)
        .putExtra(Settings.EXTRA_NOTIFICATION_LISTENER_COMPONENT_NAME, component)
    try { context.startActivity(detail) }
    catch (_: Exception) { context.startActivity(Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS)) }
}

@Composable
private fun Screen(onStart: () -> Unit, onStop: () -> Unit) {
    val context = LocalContext.current
    val prefs = remember { Prefs(context) }
    val am = remember { context.getSystemService(AudioManager::class.java) }
    val running by RouteService.running.collectAsState()
    val error by RouteService.error.collectAsState()

    var apps by remember { mutableStateOf<List<AppEntry>>(emptyList()) }
    var devices by remember { mutableStateOf(Outputs.list(am)) }
    var selected by remember { mutableStateOf(prefs.packages) }
    var device by remember { mutableStateOf(prefs.deviceId) }
    var silence by remember { mutableStateOf(prefs.silence) }
    var gain by remember { mutableFloatStateOf(prefs.gain) }
    var query by remember { mutableStateOf("") }

    LaunchedEffect(Unit) { apps = withContext(Dispatchers.IO) { Outputs.apps(context) } }
    DisposableEffect(Unit) {
        val cb = object : AudioDeviceCallback() {
            override fun onAudioDevicesAdded(added: Array<out AudioDeviceInfo>) { devices = Outputs.list(am) }
            override fun onAudioDevicesRemoved(removed: Array<out AudioDeviceInfo>) { devices = Outputs.list(am) }
        }
        am.registerAudioDeviceCallback(cb, null)
        onDispose { am.unregisterAudioDeviceCallback(cb) }
    }

    val shown = apps
        .filter { query.isBlank() || it.label.contains(query, ignoreCase = true) }
        .sortedByDescending { it.pkg in selected }
    val canStart = selected.isNotEmpty() && devices.any { it.id == device }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 20.dp, end = 20.dp, bottom = 20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        item {
            Panel {
                Label(if (running) "Routing is on" else "Routing is off", 16.sp, FontWeight.Bold)
                Label(
                    if (running) "${selected.size} app${if (selected.size == 1) "" else "s"} → ${devices.firstOrNull { it.id == device }?.name ?: "device"}"
                    else "Pick apps and an output, then start.",
                    13.sp, color = Muted,
                )
                Spacer(Modifier.height(12.dp))
                Button(
                    onClick = { if (running) onStop() else onStart() },
                    enabled = running || canStart,
                    modifier = Modifier.fillMaxWidth().height(48.dp),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = if (running) Danger else Accent),
                ) { Label(if (running) "Stop" else "Start", 15.sp, FontWeight.Bold, Color.White) }
                error?.let { Spacer(Modifier.height(8.dp)); Label(it, 13.sp, color = Danger) }
                if (!running) {
                    Spacer(Modifier.height(8.dp))
                    Label("Android will ask to record the screen. Choose \"Entire screen\": only audio is used.", 12.sp, color = Muted)
                }
            }
        }

        item {
            Panel {
                Label("Play on", 14.sp, FontWeight.Bold)
                Spacer(Modifier.height(10.dp))
                if (devices.isEmpty()) Label("No outputs found.", 13.sp, color = Muted)
                LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(devices, key = { it.id }) { d ->
                        val on = d.id == device
                        Box(
                            Modifier.clip(CircleShape)
                                .background(if (on) AccentSoft else Card)
                                .border(BorderStroke(1.dp, if (on) Accent else Line), CircleShape)
                                .clickable(enabled = !running) { device = d.id; prefs.deviceId = d.id }
                                .padding(horizontal = 14.dp, vertical = 9.dp),
                        ) { Label(d.name, 13.sp, FontWeight.SemiBold, if (on) Accent else Ink) }
                    }
                }
                Spacer(Modifier.height(14.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Label("Silence original", 14.sp, FontWeight.SemiBold)
                        Label("Stops chosen apps also playing on your normal output. Use the slider below for volume while on.", 12.sp, color = Muted)
                    }
                    Switch(
                        checked = silence, enabled = !running,
                        onCheckedChange = { silence = it; prefs.silence = it },
                        colors = SwitchDefaults.colors(checkedTrackColor = Accent),
                    )
                }
                if (silence) {
                    Spacer(Modifier.height(6.dp))
                    Slider(
                        value = gain,
                        onValueChange = { gain = it; prefs.gain = it; RouteService.setGain(it) },
                        colors = SliderDefaults.colors(thumbColor = Accent, activeTrackColor = Accent),
                    )
                }
            }
        }

        item {
            OutlinedTextField(
                value = query, onValueChange = { query = it },
                placeholder = { Label("Search apps", 14.sp, color = Muted) },
                singleLine = true, shape = RoundedCornerShape(12.dp),
                modifier = Modifier.fillMaxWidth(),
                colors = OutlinedTextFieldDefaults.colors(unfocusedContainerColor = Card, focusedContainerColor = Card,
                    unfocusedBorderColor = Line, focusedBorderColor = Accent),
            )
        }

        if (apps.isEmpty()) item { Label("Loading apps…", 13.sp, color = Muted) }

        items(shown, key = { it.pkg }) { app ->
            val on = app.pkg in selected
            AppRow(app, on, enabled = !running) {
                selected = if (on) selected - app.pkg else selected + app.pkg
                prefs.packages = selected
            }
        }
    }
}

@Composable
private fun AppRow(app: AppEntry, on: Boolean, enabled: Boolean, onToggle: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(Card)
            .clickable(enabled = enabled, onClick = onToggle)
            .alpha(if (enabled) 1f else 0.6f)
            .padding(horizontal = 14.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        AppIcon(app.icon)
        Spacer(Modifier.width(12.dp))
        Label(app.label, 15.sp, FontWeight.SemiBold, modifier = Modifier.weight(1f))
        Box(
            Modifier.size(22.dp).clip(CircleShape)
                .background(if (on) Accent else Card)
                .border(BorderStroke(1.5.dp, if (on) Accent else Line), CircleShape),
            contentAlignment = Alignment.Center,
        ) { if (on) Box(Modifier.size(8.dp).clip(CircleShape).background(Color.White)) }
    }
}

@Composable
private fun AppIcon(icon: Bitmap?) {
    if (icon != null) Image(icon.asImageBitmap(), null, Modifier.size(32.dp))
    else Box(Modifier.size(32.dp).clip(RoundedCornerShape(8.dp)).background(AccentSoft))
}

@Composable
private fun Panel(content: @Composable () -> Unit) {
    Column(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(16.dp)).background(Card)
            .border(BorderStroke(1.dp, Line), RoundedCornerShape(16.dp)).padding(16.dp),
    ) { content() }
}

@Composable
private fun Label(
    text: String,
    size: androidx.compose.ui.unit.TextUnit,
    weight: FontWeight = FontWeight.Normal,
    color: Color = Ink,
    modifier: Modifier = Modifier,
) = Text(text, modifier, color = color, fontSize = size, fontWeight = weight, fontFamily = Nunito,
        maxLines = 3, overflow = TextOverflow.Ellipsis)
