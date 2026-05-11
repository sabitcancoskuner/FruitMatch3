# 🧩 Fridge Match-3 - Match-3 Engine

A highly optimized, Match-3 puzzle engine built in Unity. 

This project demonstrates scalable game architecture, featuring a complete Model-View-Controller (MVC) separation, zero-allocation algorithms, bespoke designer tooling, and advanced VFX sequencing.

<div align="center">
  <img src="GIFs/gameplay.gif" width="250" alt="Gameplay"/>
</div>

## ✨ Core Features
* **Dynamic Combo System:** Deep powerup interactions including Rocket + Bomb, Triple Propeller Swarms, and full Board Wipes.
* **Complex Grid Geometries:** Supports non-rectangular boards (e.g., donuts, hearts) using a dynamic Composite Masking rendering pipeline.
* **"Juicy" Game Feel:** Hybrid `Ease.InQuad` and `Ease.Linear` physics for satisfying, heavy gravity, paired with integrated haptics and audio.
* **Intelligent Deadlock Resolution:** Features an automated background scanner that constantly evaluates the board for available moves, triggering a fluid board reshuffle if a true deadlock is detected so the player is never stuck.

<div align="center">
  <img src="GIFs/deadlock.gif" width="250" alt="Deadlock"/>
</div>

## 🛠️ Technical Highlights (Under the Hood)
This engine was engineered with mobile performance and clean architecture as the top priorities.

* **Strict MVC Architecture:** The "Math Brain" (`PowerupProcessor`, `MatchScanner`) is 100% decoupled from the Visuals, allowing for lightning-fast calculations without Unity `Update()` overhead.
* **Flat Memory Footprint:** Full Object Pooling implementation across all game pieces, Particle Systems, and LineRenderers prevents Garbage Collection spikes during massive board-wipe combos.
* **High-Performance Rendering:** Driven by **PrimeTween** for zero-allocation, chained animation sequences and precise Coroutines instead of costly `Update()` frame checks.

## 🎨 Custom Level Editor Tool
To speed up the level design pipeline, I engineered a custom visual editor inside Unity. 

Instead of manually typing array values, designers can visually "paint" playable tiles, spawn constraints, and obstacles directly onto a grid. The tool automatically handles the math and serializes the board state into `ScriptableObjects` (`LevelDataSO`), ensuring a frictionless workflow between engineering and design.

<div align="center">
  <img src="GIFs/editor.gif" width="1000" alt="Custom Editor"/>
</div>
