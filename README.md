\# AR Perspective Assist: A Real-Time 2-Point Perspective Drawing Guide



An Android Augmented Reality (AR) mobile application developed in \*\*Unity 6 (`6000.6.9f1`)\*\* using \*\*C#\*\*, \*\*AR Foundation\*\*, \*\*Google ARCore\*\*, and custom \*\*HLSL shaders\*\*.



\## Proponents

\- Buena, Vince Harvey

\- Carillo, Harvey

\- Marqueses, Mant Ian

\- Palaganas, Anthony

\- Rafael, Sebastian

\- Villanueva, Arjay



\*\*Institution:\*\* Mabini Colleges, Inc. — College of Computer Studies Department (Daet, Camarines Norte)  

\*\*Course:\*\* Application Development and Emergent Technologies  

\*\*Instructor:\*\* Mr. Aeron Dave Corral Enova  

\*\*Documentation Repository:\*\* https://github.com/ANTHONY19-spec/ar-perspective-assist-docs



\---



\## System Architecture \& Source Modules (`Assets/`)

\- \*\*AR Grid \& Surface Alignment:\*\* `PerspectiveAssistManager.cs`, `PerspectiveGridGenerator.cs`, `PerspectiveModeController.cs`, `PaperHorizonAligner.cs`, `RoomSurfaceGrid.cs`

\- \*\*Photo Tracing \& GPU White-Key Shader:\*\* `TraceModeController.cs`, `TraceWhiteKey.shader`, `NativeGallery` plugin, `Assets/TraceSketches/`

\- \*\*UI, Onboarding \& Tracking Diagnostics:\*\* `SideDockUIStyler.cs`, `ThesisUXPolish.cs`, `GuideDotsController.cs`



\## Dependencies \& Database Architecture

\- \*\*Engine \& Packages (`Packages/manifest.json`):\*\* Unity `6000.6.9f1` (URP), `com.unity.xr.arfoundation`, `com.unity.xr.arcore`, TextMeshPro, and `NativeGallery`.

\- \*\*Database / Storage:\*\* Stateless on-device runtime architecture (no external database); spatial anchors (`ARAnchor`) and gallery textures (`Texture2D`) reside in volatile runtime memory during active sessions.



\## Setup \& Android APK Deployment

1\. Clone this repository and open the root folder in \*\*Unity Hub\*\* using \*\*Unity `6000.6.9f1`\*\*.

2\. Open `Assets/Scenes/SampleScene.unity`.

3\. Under \*\*Edit → Project Settings → XR Plug-in Management (Android tab)\*\*, confirm \*\*Google ARCore\*\* is enabled.

4\. Connect an ARCore-supported Android phone via USB debugging and click \*\*File → Build Profiles → Build And Run\*\*. 	

