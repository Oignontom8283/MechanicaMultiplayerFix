#!/usr/bin/env python3
"""Check if Mechanica save files use patched (InvariantCulture) or unpatched (CurrentCulture) date format."""

import json
import re
from pathlib import Path

DATE_PATTERN = re.compile(r'(\d{2})/(\d{2})/(\d{4})')


def load_config():
    """Load configuration from tools.config.json"""
    config_path = Path(__file__).parent / "tools.config.json"
    if not config_path.exists():
        print(f"Error: Configuration file not found: {config_path}")
        return None
    with open(config_path, 'r', encoding='utf-8') as f:
        return json.load(f)


def detect_date_format(date_str):
    """Detect if date is InvariantCulture (MM/dd/yyyy) or CurrentCulture (dd/MM/yyyy)"""
    match = DATE_PATTERN.search(date_str)
    if not match:
        return 'unknown'
    
    first, second, _ = map(int, match.groups())
    if first > 12:
        return 'unpatched'  # dd/MM/yyyy
    if second > 12:
        return 'patched'    # MM/dd/yyyy
    return 'ambiguous'


def analyze_save(save_dir):
    """Analyze save directory for date format"""
    saveinfo = save_dir / "saveinfo.txt"
    if not saveinfo.exists():
        return None, None
    
    try:
        with open(saveinfo, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        # Extract first available date field
        for field in ['dateOfCreation', 'lastPlayedDate', 'date', 'timestamp']:
            if date_str := data.get(field):
                return detect_date_format(date_str), date_str
        return None, None
    except:
        return None, None


def main():
    config = load_config()
    if not config:
        return
    
    game_data = Path(config.get('GameData', config.get('gameDataPath', '')))
    saves_path = game_data / config.get('savesFolder', 'GameSaves')
    
    if not saves_path.exists():
        print(f"Error: Saves folder not found: {saves_path}")
        return
    
    save_dirs = sorted([d for d in saves_path.iterdir() if d.is_dir()])
    if not save_dirs:
        print("No save directories found")
        return
    
    patched_count = unpatched_count = ambiguous_count = error_count = 0
    
    for idx, save_dir in enumerate(save_dirs, 1):
        status, date_str = analyze_save(save_dir)
        save_name = f"'{save_dir.name}'"

        if status == 'patched':
            string = f"[#{idx} Patched]: "
            string = f"{string:<34} {save_name} is patched."
            print(f"{string:<80} {date_str}")
            patched_count += 1
        elif status == 'unpatched':
            string = f"[#{idx} Unpatched]: "
            string = f"{string:<34} {save_name} is NOT patched."
            print(f"{string:<80} {date_str}")
            unpatched_count += 1
        elif status == 'ambiguous':
            string = f"[#{idx} Date-Format-Ambiguous]: "
            string = f"{string:<34} {save_name} could not be processed."
            print(f"{string:<80} {date_str}")
            ambiguous_count += 1
        elif status == 'unknown':
            string = f"[#{idx} Date-Format-Not-Recognized]: "
            string = f"{string:<34} {save_name} could not be processed."
            print(f"{string:<80} {date_str if date_str else 'undefined'}")
            error_count += 1
        else:
            string = f"[#{idx} Read-Error]: "
            string = f"{string:<34} {save_name} could not be processed."
            print(f"{string:<80} undefined")
            error_count += 1
    
    print()
    readable_count = patched_count + unpatched_count + ambiguous_count
    print(f"{patched_count}/{readable_count} saves are patched!")
    if ambiguous_count > 0:
        print(f"{ambiguous_count} saves have ambiguous date format (cannot determine).")
    if error_count > 0:
        print(f"{error_count} saves could not be processed.")


if __name__ == "__main__":
    main()