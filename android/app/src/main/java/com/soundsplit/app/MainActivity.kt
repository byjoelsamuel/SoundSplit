package com.soundsplit.app

import android.Manifest
import android.app.Activity
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
import androidx.compose.material3.Button
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
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
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
        setContent {
            MaterialTheme(
                colorScheme = lightColorScheme(primary = Accent, background = Bg, surface = Card, onSurface = Ink),
                typography = Typography(bodyLarge = TextStyle(fontFamily = Nunito, fontSize = 15.sp)),
            ) { Screen(onStart = ::start, onStop = { RouteService.stop(this) }) }
        }
    }

    private fun start() {
        val perms = mutableListOf(Manifest.permission.RECORD_AUDIO)
        if (Build.VERSION.SDK_INT >= 33) perms += Manifest.permission.POST_NOTIFICATIONS
        permissions.launch(perms.toTypedArray())
    }
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
        modifier = Modifier.fillMaxSize().background(Bg).statusBarsPadding().navigationBarsPadding(),
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    Modifier.size(36.dp).clip(RoundedCornerShape(10.dp))
                        .background(Brush.linearGradient(listOf(Color(0xFF4F46E5), Color(0xFF06B6D4)))),
                    contentAlignment = Alignment.Center,
                ) { Image(painterResource(R.drawable.ic_glyph), null, Modifier.size(24.dp)) }
                Spacer(Modifier.width(12.dp))
                Column {
                    Label("SoundSplit", 20.sp, FontWeight.Bold)
                    Label("Play chosen apps on another output", 13.sp, color = Muted)
                }
            }
        }

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
