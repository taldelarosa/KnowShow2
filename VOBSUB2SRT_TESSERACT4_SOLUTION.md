# VobSub2SRT - Tesseract 4 Compatibility Solution

**Status**: ✅ RESOLVED  
**Date**: 2025-10-20  
**Solution**: Using Tesseract 4.1.1 from Debian Bullseye with PR #101 patches

## Problem Summary

The vobsub2srt tool is incompatible with Tesseract 5.x (Debian Bookworm default). Original compilation errors:

```
error: 'TessBaseAPI' has not been declared
error: 'constexpr' does not name a type
```

**Root cause**: vobsub2srt was last updated in 2015 and uses deprecated Tesseract 2.x/3.x APIs.

## Implemented Solution

### 1. APT Pinning for Tesseract 4
Install Tesseract 4.1.1 from Debian Bullseye using APT pinning:

```dockerfile
# Add Debian Bullseye repository for Tesseract 4.x
RUN echo "deb http://deb.debian.org/debian bullseye main" >> /etc/apt/sources.list.d/bullseye.list \
    && echo "Package: *\nPin: release n=bullseye\nPin-Priority: 100" > /etc/apt/preferences.d/bullseye \
    && echo "Package: tesseract-ocr* libtesseract*\nPin: release n=bullseye\nPin-Priority: 900" >> /etc/apt/preferences.d/bullseye
```

**Benefits:**
- Keeps Debian Bookworm as base OS (for .NET 8 compatibility)
- Selectively installs only Tesseract 4.1.1 packages from Bullseye
- All other packages remain from Bookworm

### 2. Apply VobSub2SRT PR #101 Patches
PR #101 adds Tesseract 4 support with three key changes:

1. **C++11 Compilation**: Adds `-std=gnu++11` flag
2. **Image Inversion**: Inverts subtitle images (dark text on light background for Tesseract 4)
3. **climits Header**: Adds missing `#include <climits>` for `UINT_MAX`

```dockerfile
&& curl -L https://patch-diff.githubusercontent.com/raw/ruediger/VobSub2SRT/pull/101.patch | git apply
```

## Why This Works

- **Tesseract 4.1.1** is the newest version with API compatibility
- **PR #101** was tested and merged (unreleased) specifically for Tesseract 4
- **VobSub functionality** fully operational (core requirement)

## Testing

Verify vobsub2srt is installed:

```bash
docker run --rm knowshow-episodeidentifier:latest vobsub2srt --version
```

Expected output: `vobsub2srt 1.0pre7`

## Alternative Approaches Considered

| Approach | Status | Reason |
|----------|--------|--------|
| Use Tesseract 5.x | ❌ Rejected | vobsub2srt incompatible, would require major code rewrite |
| Use Tesseract 3.x | ❌ Rejected | Deprecated, harder to find packages |
| Fork vobsub2srt for Tesseract 5 | ❌ Rejected | Weeks of development effort |
| Use BDSup2Sub | ❌ Rejected | Doesn't support VobSub format |
| Disable vobsub2srt | ❌ Rejected | VobSub is core functionality |

## Impact

- ✅ VobSub subtitle processing fully functional
- ✅ Compatible with existing C# code (VobSubOcrService)
- ✅ Maintains cross-platform Docker deployment
- ✅ Text and PGS subtitles unaffected
- ⚠️  Uses Tesseract 4.1.1 instead of 5.x (newest compatible version)

## References

- Original vobsub2srt repo: <https://github.com/ruediger/VobSub2SRT>
- PR #101 (Tesseract 4 support): <https://github.com/ruediger/VobSub2SRT/pull/101>
- Issue #102 (Tesseract 4 build): <https://github.com/ruediger/VobSub2SRT/issues/102>
- Issue #106 (Tesseract 5 incompatibility): <https://github.com/ruediger/VobSub2SRT/issues/106>
