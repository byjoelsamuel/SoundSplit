package com.soundsplit.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class VersionsTest {
    // Fault: comparing versions as text ("1.10.0" < "1.9.0") would hide real updates.
    @Test fun `two-digit parts compare as numbers`() = assertTrue(Versions.isNewer("v1.10.0", "1.9.0"))

    // Fault: offering the version you already have causes an endless update prompt.
    @Test fun `same version is not an update`() = assertFalse(Versions.isNewer("v1.3.0", "1.3.0"))

    // Fault: offering an older release (e.g. a re-published old tag) downgrades the app.
    @Test fun `older version is not an update`() = assertFalse(Versions.isNewer("v1.2.0", "1.3.0"))

    @Test fun `missing parts count as zero`() {
        assertFalse(Versions.isNewer("1.3", "1.3.0"))
        assertTrue(Versions.isNewer("1.3.1", "1.3"))
    }

    // Fault: a malformed tag crashing the update check (or being treated as newer).
    @Test fun `garbage is never newer`() {
        assertFalse(Versions.isNewer("latest", "1.0.0"))
        assertFalse(Versions.isNewer("v1.x", "1.0.0"))
    }

    @Test fun `prefix is stripped`() = assertEquals("1.3.0", Versions.clean(" v1.3.0 "))
}

class TimeFormatTest {
    @Test fun minutes() = assertEquals("1:05", TimeFormat.mmss(65_000))
    @Test fun hours() = assertEquals("1:02:05", TimeFormat.mmss(3_725_000))
    // Fault: negative positions (seen right after a seek) printing "-0:01".
    @Test fun negative() = assertEquals("0:00", TimeFormat.mmss(-1_000))
}
