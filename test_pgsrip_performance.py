#!/usr/bin/env python3
"""
Simple script to test pgsrip performance on a specific video file
"""
import time
import sys
from pathlib import Path

try:
    from pgsrip.ripper import PgsToSrtRipper
    from pgsrip.options import Options
except ImportError:
    print("ERROR: pgsrip not installed. Install with: pip install pgsrip")
    sys.exit(1)

def test_pgsrip_extraction(video_path):
    """Test pgsrip subtitle extraction and measure performance"""
    print(f"Testing pgsrip on: {video_path}")
    print("=" * 80)
    
    # Try different path formats
    paths_to_try = [
        video_path,
        video_path.replace('\\\\', '/mnt/'),
        video_path.replace('\\', '/'),
    ]
    
    # Add void-specific path if applicable
    if 'void' in video_path:
        void_path = video_path.split('void')[1].replace('\\', '/')
        paths_to_try.append(f"/mnt/void{void_path}")
    
    actual_path = None
    for path in paths_to_try:
        if path and Path(path).exists():
            actual_path = path
            print(f"Found file at: {actual_path}")
            break
    
    if not actual_path:
        print("ERROR: File not found at any of these locations:")
        for path in paths_to_try:
            if path:
                print(f"  - {path}")
        print("\nPlease provide a local file path or ensure the network share is mounted.")
        return
    
    try:
        start_time = time.time()
        
        # Create pgsrip options and ripper
        print("Creating pgsrip ripper...")
        options = Options()
        options.language = 'eng'  # Set language to English
        
        ripper = PgsToSrtRipper(options)
        
        # Extract subtitles using pgsrip
        print("Starting pgsrip extraction...")
        result = ripper.rip(actual_path)
        
        end_time = time.time()
        extraction_time = end_time - start_time
        
        print(f"Extraction completed in {extraction_time:.2f} seconds")
        
        if result and hasattr(result, 'subtitle_items'):
            subtitles = result.subtitle_items
            print(f"Number of subtitle entries: {len(subtitles)}")
            
            if subtitles:
                print("\nFirst few subtitle entries:")
                for i, sub in enumerate(subtitles[:5]):
                    print(f"  {i+1}. {sub.start} --> {sub.end}")
                    print(f"     {sub.text}")
                    print()
                
                # Calculate total text length
                total_text = '\n'.join([sub.text for sub in subtitles])
                print(f"Total text length: {len(total_text)} characters")
                print(f"Average characters per subtitle: {len(total_text) / len(subtitles):.1f}")
                
                # Show timing distribution
                durations = []
                for sub in subtitles:
                    if hasattr(sub, 'start') and hasattr(sub, 'end'):
                        # Convert time format if needed
                        duration = 3.0  # Default fallback
                        try:
                            # This might need adjustment based on actual time format
                            duration = float(sub.end) - float(sub.start) if isinstance(sub.start, (int, float)) else 3.0
                        except:
                            pass
                        durations.append(duration)
                
                if durations:
                    avg_duration = sum(durations) / len(durations)
                    print(f"Average subtitle duration: {avg_duration:.2f} seconds")
        else:
            print("No subtitles extracted or unexpected result format!")
            print(f"Result type: {type(result)}")
            if hasattr(result, '__dict__'):
                print(f"Result attributes: {list(result.__dict__.keys())}")
            
    except Exception as e:
        print(f"ERROR during extraction: {e}")
        print(f"Exception type: {type(e).__name__}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    # The specific file you want to test
    video_file = "/mnt/startrek_tos/STAR_TREK_S1D3-CBcCcj/Star Trek Season 1- Disc 3_t14.mkv"
    
    test_pgsrip_extraction(video_file)