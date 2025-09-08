#!/usr/bin/env bash
# KnowShow2 - Comprehensive prerequisite checker and installer for fresh Linux systems
# This script will check for and install all required dependencies for the Episode Identifier application
#
# Usage: ./scripts/setup-prerequisites.sh [--install] [--check-only] [--verbose]
#   --install     : Automatically install missing dependencies (requires sudo)
#   --check-only  : Only check for dependencies, don't install anything
#   --verbose     : Show detailed information about each check
#   --help        : Show this help message

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script options
INSTALL_MODE=false
CHECK_ONLY=false
VERBOSE=false

# Parse command line arguments
for arg in "$@"; do
    case "$arg" in
        --install) INSTALL_MODE=true ;;
        --check-only) CHECK_ONLY=true ;;
        --verbose) VERBOSE=true ;;
        --help|-h) 
            echo "KnowShow2 - Prerequisite Setup Script"
            echo "======================================"
            echo
            echo "This script will check for and install all required dependencies for the Episode Identifier application"
            echo
            echo "Usage: $0 [--install] [--check-only] [--verbose]"
            echo "  --install     : Automatically install missing dependencies (requires sudo)"
            echo "  --check-only  : Only check for dependencies, don't install anything"
            echo "  --verbose     : Show detailed information about each check"
            echo "  --help        : Show this help message"
            echo
            echo "Examples:"
            echo "  $0 --check-only          # Check what's missing without installing"
            echo "  $0 --install             # Install all missing dependencies"
            echo "  $0 --install --verbose   # Install with detailed output"
            exit 0
            ;;
        *) echo "Unknown argument: $arg. Use --help for usage."; exit 1 ;;
    esac
done

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_verbose() {
    if $VERBOSE; then
        echo -e "${BLUE}[VERBOSE]${NC} $1"
    fi
}

# Check if running on supported OS
check_os() {
    log_info "Checking operating system compatibility..."
    
    if [[ ! -f /etc/os-release ]]; then
        log_error "Cannot determine OS. This script supports Ubuntu/Debian systems."
        exit 1
    fi
    
    source /etc/os-release
    case "$ID" in
        ubuntu|debian)
            log_success "Detected $PRETTY_NAME - supported OS"
            log_verbose "OS ID: $ID, Version: $VERSION_ID"
            ;;
        *)
            log_warning "Detected $PRETTY_NAME - may not be fully supported"
            log_warning "This script is optimized for Ubuntu/Debian. Continue at your own risk."
            read -p "Continue anyway? [y/N]: " -n 1 -r
            echo
            if [[ ! $REPLY =~ ^[Yy]$ ]]; then
                exit 1
            fi
            ;;
    esac
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check package installation
check_package() {
    local package_name="$1"
    local command_to_check="$2"
    local description="$3"
    
    log_verbose "Checking for $description ($package_name)..."
    
    if command_exists "$command_to_check"; then
        local version=$($command_to_check --version 2>/dev/null | head -n 1 || echo "unknown version")
        log_success "$description is installed: $version"
        return 0
    else
        log_warning "$description ($command_to_check) is not installed"
        return 1
    fi
}

# Install package using apt
install_package() {
    local packages="$1"
    local description="$2"
    
    if $INSTALL_MODE; then
        log_info "Installing $description..."
        sudo apt-get update -qq
        sudo apt-get install -y $packages
        log_success "$description installed successfully"
    else
        log_warning "Would install: $packages (use --install to actually install)"
    fi
}

# Check and install system packages
check_system_dependencies() {
    log_info "Checking system dependencies..."
    
    local missing_packages=()
    local missing_descriptions=()
    
    # Essential build tools
    if ! command_exists "curl"; then
        missing_packages+=("curl")
        missing_descriptions+=("curl (for downloading dependencies)")
    fi
    
    if ! command_exists "wget"; then
        missing_packages+=("wget")
        missing_descriptions+=("wget (for downloading files)")
    fi
    
    if ! command_exists "git"; then
        missing_packages+=("git")
        missing_descriptions+=("git (version control)")
    fi
    
    # Video processing tools
    if ! check_package "ffmpeg" "ffmpeg" "FFmpeg (video processing)"; then
        missing_packages+=("ffmpeg")
        missing_descriptions+=("FFmpeg (video processing)")
    fi
    
    if ! check_package "mkvtoolnix-cli" "mkvextract" "MKVToolNix (container manipulation)"; then
        missing_packages+=("mkvtoolnix")
        missing_descriptions+=("MKVToolNix (container manipulation)")
    fi
    
    # OCR dependencies
    if ! check_package "tesseract-ocr" "tesseract" "Tesseract OCR (text recognition)"; then
        missing_packages+=("tesseract-ocr")
        missing_descriptions+=("Tesseract OCR (text recognition)")
    fi
    
    if ! dpkg -l | grep -q tesseract-ocr-eng; then
        missing_packages+=("tesseract-ocr-eng")
        missing_descriptions+=("Tesseract English language pack")
    fi
    
    # Additional useful language packs
    if ! dpkg -l | grep -q tesseract-ocr-spa; then
        missing_packages+=("tesseract-ocr-spa")
        missing_descriptions+=("Tesseract Spanish language pack")
    fi
    
    if ! dpkg -l | grep -q tesseract-ocr-deu; then
        missing_packages+=("tesseract-ocr-deu")
        missing_descriptions+=("Tesseract German language pack")
    fi
    
    if ! dpkg -l | grep -q tesseract-ocr-fra; then
        missing_packages+=("tesseract-ocr-fra")
        missing_descriptions+=("Tesseract French language pack")
    fi
    
    # Database tools
    if ! check_package "sqlite3" "sqlite3" "SQLite3 (database)"; then
        missing_packages+=("sqlite3")
        missing_descriptions+=("SQLite3 (database)")
    fi
    
    # Development tools
    if ! command_exists "unzip"; then
        missing_packages+=("unzip")
        missing_descriptions+=("unzip (archive extraction)")
    fi
    
    if ! command_exists "ca-certificates"; then
        missing_packages+=("ca-certificates")
        missing_descriptions+=("ca-certificates (SSL certificates)")
    fi
    
    # Report missing packages
    if [ ${#missing_packages[@]} -eq 0 ]; then
        log_success "All system packages are installed"
    else
        log_warning "Missing ${#missing_packages[@]} system packages:"
        for i in "${!missing_descriptions[@]}"; do
            echo "  - ${missing_descriptions[$i]}"
        done
        
        if ! $CHECK_ONLY; then
            install_package "${missing_packages[*]}" "missing system packages"
        fi
    fi
}

# Check and install .NET SDK
check_dotnet() {
    log_info "Checking .NET SDK..."
    
    if command_exists "dotnet"; then
        local dotnet_version=$(dotnet --version 2>/dev/null || echo "unknown")
        log_success ".NET SDK is installed: $dotnet_version"
        
        # Check if .NET 8.0 SDK is available
        if dotnet --list-sdks | grep -q "8\."; then
            log_success ".NET 8.0 SDK is available"
        else
            log_warning ".NET 8.0 SDK not found. Available SDKs:"
            dotnet --list-sdks | sed 's/^/    /'
        fi
    else
        log_warning ".NET SDK is not installed"
        
        if $INSTALL_MODE && ! $CHECK_ONLY; then
            log_info "Installing .NET SDK..."
            
            # Download and install Microsoft package repository
            wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
            sudo dpkg -i packages-microsoft-prod.deb
            rm packages-microsoft-prod.deb
            
            # Install .NET SDK
            sudo apt-get update
            sudo apt-get install -y dotnet-sdk-8.0
            
            log_success ".NET SDK 8.0 installed successfully"
        else
            log_warning "Would install .NET SDK 8.0 (use --install to actually install)"
        fi
    fi
}

# Check and install Python with uv
check_python() {
    log_info "Checking Python environment..."
    
    # Check Python installation
    if command_exists "python3"; then
        local python_version=$(python3 --version 2>/dev/null || echo "unknown")
        log_success "Python is installed: $python_version"
    else
        log_warning "Python3 is not installed"
        if $INSTALL_MODE && ! $CHECK_ONLY; then
            install_package "python3 python3-pip python3-venv" "Python3 and pip"
        fi
    fi
    
    # Check uv (fast Python package installer)
    if command_exists "uv"; then
        local uv_version=$(uv --version 2>/dev/null || echo "unknown")
        log_success "uv (fast Python package manager) is installed: $uv_version"
    else
        log_warning "uv is not installed (recommended for faster Python package installation)"
        
        if $INSTALL_MODE && ! $CHECK_ONLY; then
            log_info "Installing uv..."
            curl -LsSf https://astral.sh/uv/install.sh | sh
            source $HOME/.cargo/env
            log_success "uv installed successfully"
        else
            log_warning "Would install uv (use --install to actually install)"
        fi
    fi
}

# Check and install pgsrip
check_pgsrip() {
    log_info "Checking pgsrip (advanced PGS subtitle processor)..."
    
    if command_exists "pgsrip"; then
        local pgsrip_version=$(pgsrip --version 2>/dev/null || echo "unknown")
        log_success "pgsrip is installed: $pgsrip_version"
    else
        log_warning "pgsrip is not installed"
        
        if $INSTALL_MODE && ! $CHECK_ONLY; then
            log_info "Installing pgsrip..."
            
            if command_exists "uv"; then
                uv pip install --system pgsrip
            elif command_exists "pip3"; then
                pip3 install pgsrip
            else
                log_error "Cannot install pgsrip: neither uv nor pip3 is available"
                return 1
            fi
            
            log_success "pgsrip installed successfully"
        else
            log_warning "Would install pgsrip (use --install to actually install)"
        fi
    fi
}

# Install enhanced Tesseract training data
install_enhanced_tessdata() {
    log_info "Checking enhanced Tesseract training data..."
    
    local tessdata_best_dir="/usr/share/tessdata_best"
    
    if [[ -d "$tessdata_best_dir" ]]; then
        log_success "Enhanced Tesseract training data is installed"
    else
        log_warning "Enhanced Tesseract training data not found"
        
        if $INSTALL_MODE && ! $CHECK_ONLY; then
            log_info "Installing enhanced Tesseract training data..."
            
            cd /tmp
            git clone --depth 1 https://github.com/tesseract-ocr/tessdata_best.git
            sudo mv tessdata_best "$tessdata_best_dir"
            
            # Add to environment
            if ! grep -q "TESSDATA_PREFIX" ~/.bashrc; then
                echo "export TESSDATA_PREFIX=$tessdata_best_dir" >> ~/.bashrc
                log_info "Added TESSDATA_PREFIX to ~/.bashrc"
            fi
            
            export TESSDATA_PREFIX="$tessdata_best_dir"
            log_success "Enhanced Tesseract training data installed"
        else
            log_warning "Would install enhanced Tesseract training data (use --install to actually install)"
        fi
    fi
}

# Verify project can build
verify_project() {
    log_info "Verifying project can build..."
    
    local project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
    local core_project="$project_root/src/EpisodeIdentifier.Core"
    
    if [[ ! -f "$core_project/EpisodeIdentifier.Core.csproj" ]]; then
        log_warning "Project file not found at $core_project"
        return
    fi
    
    if command_exists "dotnet"; then
        log_verbose "Attempting to restore and build project..."
        cd "$core_project"
        
        if dotnet restore >/dev/null 2>&1; then
            log_success "Project dependencies restored successfully"
            
            if dotnet build >/dev/null 2>&1; then
                log_success "Project builds successfully"
            else
                log_warning "Project build failed - check dependencies"
            fi
        else
            log_warning "Failed to restore project dependencies"
        fi
    else
        log_warning "Cannot verify project build - .NET SDK not available"
    fi
}

# Main execution
main() {
    echo "=============================================="
    echo "  KnowShow2 - Prerequisite Setup Script"
    echo "=============================================="
    echo
    
    # Parse conflicting options
    if $INSTALL_MODE && $CHECK_ONLY; then
        log_error "Cannot use --install and --check-only together"
        exit 1
    fi
    
    # Check OS compatibility
    check_os
    echo
    
    # Check all dependencies
    check_system_dependencies
    echo
    
    check_dotnet
    echo
    
    check_python
    echo
    
    check_pgsrip
    echo
    
    install_enhanced_tessdata
    echo
    
    # Only verify if not just checking
    if ! $CHECK_ONLY; then
        verify_project
        echo
    fi
    
    # Final summary
    echo "=============================================="
    echo "  Setup Complete"
    echo "=============================================="
    
    if $CHECK_ONLY; then
        log_info "Check completed. Use --install to install missing dependencies."
    elif $INSTALL_MODE; then
        log_success "All dependencies have been installed and verified."
        log_info "You may need to restart your terminal or run 'source ~/.bashrc' to use new environment variables."
    else
        log_info "Dry run completed. Use --install to actually install missing dependencies."
    fi
    
    echo
    log_info "Next steps:"
    echo "  1. Build the project: cd src/EpisodeIdentifier.Core && dotnet build"
    echo "  2. Run tests: dotnet test"
    echo "  3. See README.md for usage examples"
}

# Run main function
main "$@"
