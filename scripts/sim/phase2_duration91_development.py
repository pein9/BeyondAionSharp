#!/usr/bin/env python3
"""Pair the duration-aware 91% potion variant on both Mau legs."""

from pathlib import Path

import phase2_threat91_development as paired


paired.TAG = "duration91-v1"
paired.OUTPUT = Path(__file__).resolve().parents[2] / "docs/bot-learning-phase2-duration91-development.json"

if __name__ == "__main__":
    paired.main()
