PPW Curves
====
An example implementation for PPW Curves: A C^2 Interpolating Spline with Hyperbolic Blending of Rational Bézier Curves.

## Requirements
- Unity 2021.3.3f1
  - It should work with some older/newer versions

## Quick start 
1. Clone this repository
2. Open the scene `Assets/Scenes/Drawer.unity` with Unity Editor
3. Play it and click the Game view to put control points

## Control guide

### Mouse/Keyboard inputs

In the following grid, the "@" and "/" represent "click a control point (CP)" and "click a path" respectively.  

| Control                  | Action                                    |
|--------------------------|-------------------------------------------|
| Esc                      | Open/Close help                           |
| Right Click and Drag     | Move canvas                               |
| Wheel                    | Zoom in/out                               |
| Left Click               | Create paths                              |
| Left DoubleClick         | End path creation                         |
| Ctrl + @                 | Move CP                                   |
| Ctrl + /                 | Add CP                                    |
| Shift + @                | Weight CP                                 |
| Shift + /                | Weight segment                            |
| Alt + @                  | Delete CP                                 |
| Alt + /                  | Delete segment                            |
| Ctrl + Alt + @           | Sharpen CP                                |
| Ctrl + Shift + @         | Connect CP                                |
| Ctrl + Shift + /         | Duplicate path                            |
| Shift + Alt + @,/        | Move path                                 |
| Ctrl + Shift + Alt + @,/ | Delete path                               |
| Ctrl + Q                 | Quit app                                  |
| F1                       | Export SVG                                |
| F2                       | Screenshot (`Assets/Results/SS/...`)      |
| F3                       | FullScreen (standalone-only)              |
| Shift + Z/Y              | Undo/Redo (experimental, editor-only)     |
| Ctrl + Z/Y               | Undo/Redo (experimental, standalone-only) |
| 0~9                      | Input preset shapes                       |

### GUI inputs

| UI                                | Action                                                                            |
|-----------------------------------|-----------------------------------------------------------------------------------|
| Input field and Save/Load buttons | Save/Load current paths as a json file to<br/> `Assets/Results/JSON/<input>.json` |
| Curvature Solver                  | This is for rational κ-Curves, so don't take care                                 |
| Show Control points               | Show/hide control points on paths                                                 |
| Show Control polygon              | Show/hide control polygons (the paths before being blended)                       |
| Show Weight                       | Show/hide weight value of each control point                                      |
| Show Curvature                    | Show/hide curvature-indicator for paths<br/>Zooming in/out canvas can redraw them |

## For code readers
The core implementation of PPW Curves is `Assets/Plugins/RUtil/Scripts/Runtime/Curve/PPWCurve.cs`. 
The drawer `Assets/Scripts/PathDrawer/PPWCurveDrawer.cs` constructs and inputs curve data into `PPWCurve.CalcAll`.
Calculation results will be stored in the input PPWCurve.CurveData instance.
