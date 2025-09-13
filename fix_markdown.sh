#!/bin/bash

# Script to fix common markdown linting issues

echo "Fixing markdown linting issues..."

# Get list of all markdown files
find . -name "*.md" -type f | while read -r file; do
    echo "Processing: $file"
    
    # Create a temporary file
    temp_file=$(mktemp)
    
    # Read the file and apply fixes
    {
        # Track if we're in a code block
        in_code_block=false
        prev_line=""
        prev_line_type=""
        
        while IFS= read -r line || [[ -n "$line" ]]; do
            current_line_type=""
            
            # Detect code block boundaries
            if [[ $line =~ ^\`\`\` ]]; then
                if [[ $in_code_block == false ]]; then
                    in_code_block=true
                    current_line_type="code_start"
                else
                    in_code_block=false
                    current_line_type="code_end"
                fi
            fi
            
            # Remove trailing spaces (MD009)
            line=$(echo "$line" | sed 's/[[:space:]]*$//')
            
            # Detect heading
            if [[ $line =~ ^#+[[:space:]] ]]; then
                current_line_type="heading"
            fi
            
            # Detect list item
            if [[ $line =~ ^[[:space:]]*[-*+][[:space:]] ]] || [[ $line =~ ^[[:space:]]*[0-9]+\.[[:space:]] ]]; then
                current_line_type="list"
            fi
            
            # Add blank line before headings if previous line is not blank (MD022)
            if [[ $current_line_type == "heading" ]] && [[ -n "$prev_line" ]]; then
                echo ""
            fi
            
            # Add blank line before code blocks (MD031)
            if [[ $current_line_type == "code_start" ]] && [[ -n "$prev_line" ]]; then
                echo ""
            fi
            
            # Add blank line before lists if previous line is not blank (MD032)
            if [[ $current_line_type == "list" ]] && [[ -n "$prev_line" ]] && [[ $prev_line_type != "list" ]]; then
                echo ""
            fi
            
            # Output the current line
            echo "$line"
            
            # Add blank line after headings if next line is not blank (MD022)
            # Add blank line after code blocks (MD031)
            # Add blank line after lists if next line is not blank (MD032)
            if [[ $current_line_type == "heading" ]] || [[ $current_line_type == "code_end" ]] || ([[ $prev_line_type == "list" ]] && [[ $current_line_type != "list" ]] && [[ -n "$line" ]]); then
                echo ""
            fi
            
            prev_line="$line"
            prev_line_type="$current_line_type"
            
        done
        
        # Ensure file ends with single newline (MD047)
        
    } < "$file" > "$temp_file"
    
    # Replace original file with fixed version
    mv "$temp_file" "$file"
done

echo "Markdown fixing complete!"