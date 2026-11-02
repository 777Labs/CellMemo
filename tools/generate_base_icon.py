import os
import sys
from process_icon import create_procedural_raw_icon

if __name__ == "__main__":
    base_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    raw_path = os.path.join(base_dir, "assets", "raw_icon.png")
    os.makedirs(os.path.dirname(raw_path), exist_ok=True)
    create_procedural_raw_icon(raw_path, 1024)
    print(f"[SUCCESS] Base procedural raw icon generated at: {raw_path}")
