# KnowShow2 - Complete Setup Guide

This guide will help you set up KnowShow2 (Episode Identifier) on a fresh Linux system.

## Quick Start (Recommended)

For most users, the automated setup script is the easiest way to get started:

```bash

# 1. Clone the repository







git clone https://github.com/taldelarosa/KnowShow2.git
cd KnowShow2

# 2. Run the setup script to check dependencies







./scripts/setup-prerequisites.sh --check-only

# 3. Install all dependencies automatically







./scripts/setup-prerequisites.sh --install

# 4. Build the project







cd src/EpisodeIdentifier.Core
dotnet build

# 5. Run tests to verify everything works







dotnet test

# 6. Try a quick test (if you have a video file)







dotnet run -- --help
```

## What Gets Installed

The setup script will install and configure:

### System Packages

- **ffmpeg** - Video processing and format validation
- **mkvtoolnix** - MKV container manipulation (mkvextract)
- **tesseract-ocr** - Optical Character Recognition engine
- **tesseract language packs** - English, Spanish, German, French
- **sqlite3** - Database for storing subtitle hashes
- **curl, wget, git** - Download and version control tools
- **unzip** - Archive extraction
- **ca-certificates** - SSL certificate validation

### Programming Environments

- **.NET 8.0 SDK** - Required for building and running the application
- **Python 3** - Required for pgsrip and advanced processing
- **uv** - Fast Python package manager (alternative to pip)

### Specialized Tools

- **pgsrip** - Advanced PGS subtitle processor (provides 90%+ OCR accuracy)
- **Enhanced Tesseract training data** - Improved OCR models for better text recognition

## Manual Installation (Advanced Users)

If you prefer to install dependencies manually or need to troubleshoot:

### 1. Install System Dependencies

**Ubuntu/Debian:**

```bash

# Update package list







sudo apt-get update

# Essential tools







sudo apt-get install -y curl wget git unzip ca-certificates

# Video processing







sudo apt-get install -y ffmpeg mkvtoolnix

# OCR and database







sudo apt-get install -y tesseract-ocr tesseract-ocr-eng tesseract-ocr-spa tesseract-ocr-deu tesseract-ocr-fra sqlite3
```

**Other Linux distributions:**

- **RHEL/CentOS/Fedora:** Use `dnf install` or `yum install`
- **Arch Linux:** Use `pacman -S`
- **openSUSE:** Use `zypper install`

### 2. Install .NET 8.0 SDK

```bash

# Download Microsoft package repository configuration







wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET SDK







sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# Verify installation







dotnet --version
```

### 3. Install Python and uv

```bash

# Install Python







sudo apt-get install -y python3 python3-pip python3-venv

# Install uv (fast package manager)







curl -LsSf https://astral.sh/uv/install.sh | sh
source $HOME/.cargo/env

# Verify installation







python3 --version
uv --version
```

### 4. Install pgsrip

```bash

# Using uv (recommended - faster)







uv pip install --system pgsrip

# Or using pip







pip3 install pgsrip

# Verify installation







pgsrip --version
```

### 5. Install Enhanced Tesseract Data (Optional but Recommended)

```bash

# Clone enhanced training data







cd /tmp
git clone --depth 1 https://github.com/tesseract-ocr/tessdata_best.git

# Install to system location







sudo mv tessdata_best /usr/share/tessdata_best

# Set environment variable







echo 'export TESSDATA_PREFIX=/usr/share/tessdata_best' >> ~/.bashrc
source ~/.bashrc
```

## Verification

After installation, verify everything is working:

```bash

# Check all tools are available







ffmpeg -version
mkvextract --version
tesseract --version
sqlite3 --version
dotnet --version
python3 --version
uv --version
pgsrip --version

# Build the project







cd src/EpisodeIdentifier.Core
dotnet restore
dotnet build

# Run tests







cd ../../
dotnet test tests/unit/
dotnet test tests/integration/
dotnet test tests/contract/
```

## Troubleshooting

### Setup Script Issues

**Script not executable:**

```bash
chmod +x scripts/setup-prerequisites.sh
```

**Permission denied during installation:**

```bash

# Make sure you can use sudo







sudo whoami

# Run setup with verbose output







./scripts/setup-prerequisites.sh --install --verbose
```

### Common Dependency Issues

**FFmpeg not found:**

```bash

# Check if installed







which ffmpeg
ffmpeg -version

# Install if missing







sudo apt-get install ffmpeg
```

**pgsrip installation fails:**

```bash

# Try installing with pip instead of uv







pip3 install pgsrip

# Or check Python version (requires Python 3.8+)







python3 --version
```

**.NET build fails:**

```bash

# Check .NET SDK version







dotnet --version

# Should be 8.0.x - install if missing








# See manual installation section above







```

**Tesseract OCR issues:**

```bash

# Check tesseract installation







tesseract --version
tesseract --list-langs

# Should include 'eng' at minimum








# Install language packs if missing







sudo apt-get install tesseract-ocr-eng
```

### Environment Variables

Make sure these are set correctly:

```bash

# Check environment variables







echo $TESSDATA_PREFIX
echo $PATH

# Should include paths to:








# - /usr/bin (for system tools)








# - ~/.local/bin (for user-installed Python packages)








# - ~/.cargo/bin (for uv)







```

### Test Installation

Run this simple test to verify everything works:

```bash

# Test video processing







ffmpeg -f lavfi -i testsrc=duration=1:size=320x240:rate=1 -c:v libx264 test.mp4

# Test OCR







echo "Hello World" | tesseract stdin stdout

# Test .NET







dotnet --info

# Test pgsrip







pgsrip --help

# Clean up







rm test.mp4
```

## Next Steps

Once setup is complete:

1. **Read the main README.md** for usage examples
2. **Review the quickstart guide** at `specs/002-build-an-application/quickstart.md`
3. **Try the demo commands** in the main README
4. **Check the test suite** to understand the application capabilities

## Getting Help

- **Setup issues:** Run `./scripts/setup-prerequisites.sh --check-only --verbose`
- **Build issues:** Check the project documentation in `specs/`
- **Runtime issues:** See troubleshooting section in main README.md
- **Missing features:** Check the project roadmap and specifications

## Supported Platforms

This application is primarily designed for Linux systems:

- ✅ **Ubuntu 20.04+** (fully tested)
- ✅ **Debian 10+** (should work)
- ⚠️ **Other Linux distributions** (may need manual dependency adjustment)
- ❌ **Windows** (not currently supported - WSL recommended)
- ❌ **macOS** (not currently supported)

For Windows users, we recommend using WSL (Windows Subsystem for Linux) with Ubuntu.
