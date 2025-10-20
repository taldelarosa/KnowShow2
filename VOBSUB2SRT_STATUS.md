# VobSub2SRT Status - Tesseract 4 Compatibility Solution

**Status**: ✅ RESOLVED  
**Date**: 2025-10-20  
**Solution**: Using Tesseract 4.1.1 from Debian Bullseye with PR #101 patches

## Problem Summary

The vobsub2srt tool (https://github.com/ruediger/VobSub2SRT) is incompatible with Tesseract 5.x, which is the default version in Debian Bookworm. The project was last updated in 2015 and uses deprecated Tesseract APIs.

### Original Compilation Errors with Tesseract 5.x

```
error: 'TessBaseAPI' has not been declared
error: 'constexpr' does not name a type
```

These errors occur because:
