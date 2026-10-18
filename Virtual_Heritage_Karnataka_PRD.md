# PRD — Virtual Heritage Karnataka

## 1. Product Overview

**Virtual Heritage Karnataka** is an immersive VR-based digital heritage experience that allows users to explore historically significant Karnataka temples in a navigable 3D environment.

The system presents selected temple architecture through **interactive 3D reconstruction, VR navigation, architectural-element inspection, historical information, and a simplified Then vs. Now visualization**.

The prototype prioritizes **visual quality, interaction, and educational value** over attempting to reconstruct complete temples with archaeological-level accuracy.

---

## 2. Problem Statement

Many historically significant temples in Karnataka are difficult for students and the general public to experience in depth due to geographical distance, accessibility limitations, and the complexity of their architecture.

Existing virtual heritage research demonstrates VR-based temple exploration, while identifying opportunities around:

- Multi-period architectural comparison
- Temporal **Then vs. Now** visualization
- Interactive inspection of architectural elements
- Better multimodal experiences
- Karnataka-specific heritage representation

**Virtual Heritage Karnataka** addresses these opportunities through a focused, interactive VR prototype.

---

## 3. Objectives

1. Digitally represent selected Karnataka temple architecture.
2. Allow users to explore the environment through VR.
3. Enable users to inspect important architectural elements.
4. Provide contextual historical and architectural information.
5. Demonstrate a **Then vs. Now** visualization.
6. Showcase Computer Graphics concepts such as:
   - 3D modeling
   - Transformations
   - Materials
   - Lighting
   - Shadows
   - Camera systems
   - Animation
   - Rendering
7. Demonstrate VR concepts such as:
   - Teleportation
   - Spatial interaction
   - Object selection
   - Immersive navigation

---

## 4. Target Users

### Primary
**Students and educators**

Students can use the application to understand Karnataka's temple architecture without needing to physically visit each location.

### Secondary
**Heritage enthusiasts and general users**

Users interested in Karnataka's history can explore temples interactively.

### Tertiary
**Researchers / architecture students**

The semantic inspection system can provide structured information about architectural elements.

---

## 5. MVP Scope

To keep the project feasible, the MVP will contain **3 representative Karnataka temples**, rather than attempting to recreate dozens.

Suggested prototype set:

- **Chennakeshava Temple — Belur**
- **Hoysaleswara Temple — Halebidu**
- **Virupaksha Temple — Hampi**

The system should not claim that entire temples have been archaeologically reconstructed if only selected sections/models are implemented.

### Each temple contains

- Exterior environment
- One primary architectural area
- Selected pillars/sculptures
- Basic lighting
- Information points
- Teleport locations
- Ambient audio

---

## 6. Core User Journey

```text
Launch
  ↓
Welcome Screen
  ↓
Select Temple
  ↓
Temple Introduction
  ↓
Enter VR
  ↓
Explore Temple
  ↓
Discover Architectural Element
  ↓
Inspect Element
  ↓
View Historical Information
  ↓
Toggle Then / Now
  ↓
Return to Temple Hub
  ↓
Select Another Temple
```

---

## 7. Feature Requirements

### F1 — Temple Selection Hub

Create a central virtual gallery containing three temple entrances.

Each entrance displays:

- Temple name
- Location
- Historical period
- Architectural tradition
- Short description

Example:

> **Hoysaleswara Temple**  
> Halebidu, Karnataka  
> Hoysala Architecture

The user can select a temple and enter its virtual environment.

---

### F2 — VR Navigation

The user should be able to:

- Teleport
- Move around the environment
- Look around freely
- Turn using controller
- Return to the temple hub

#### Interaction

Use a simple VR controller ray:

```text
Controller
     ↓
Teleport Ray
     ↓
Valid Surface
     ↓
Teleport
```

Avoid complicated free locomotion initially because teleportation is faster to implement and reduces motion-sickness issues.

---

### F3 — Interactive Architectural Elements

This is one of the main features.

When the user points at an interactive object:

```text
Pillar
   ↓
Highlight
   ↓
Select
   ↓
Information Panel
```

The panel displays:

- Name
- Architectural Type
- Historical Context
- Material
- Temple
- Approximate Period

Example:

> **Lathe-Turned Pillar**  
> Hoysaleswara Temple  
> Hoysala architectural tradition  
> Material: Stone  
>
> The pillar demonstrates the characteristic precision and ornamentation associated with Hoysala architecture.

---

### F4 — Architectural Inspection Mode

When an object is selected, provide:

#### Normal View

The object remains in its original position.

#### Inspect View

The object is:

- Enlarged
- Centered
- Rotatable

The user can rotate the object using the controller.

Optional:

**Wireframe Toggle**

```text
SOLID
  ↓
WIREFRAME
```

This provides a clear Computer Graphics demonstration.

---

### F5 — Then vs. Now

Implement a simple but visually impressive temporal visualization rather than a complicated historical reconstruction system.

The user presses:

> **THEN ↔ NOW**

The scene transitions between two visual states.

### NOW

Current representation.

### THEN

Historical/reconstructed representation where sufficient evidence/model assets are available.

A simple implementation can use:

- Object visibility switching
- Material changes
- Alpha blending
- Scene variants

Reconstructed elements should be clearly identified as reconstructed rather than presented as certain historical fact.

---

### F6 — Temple Information Points

Place interactive information markers around the environment.

Example:

```text
      [Gopuram]
          |
          ↓
       [INFO]
```

When selected:

> **Gopuram**  
> Monumental entrance tower associated with South Indian temple architecture.

Keep information short and readable inside VR.

---

### F7 — Historical Timeline

Each temple has a small timeline:

```text
Historical Period
      ↓
Temple Construction
      ↓
Architectural Development
      ↓
Present Day
```

A simple slider can allow users to move through the timeline.

---

### F8 — Spatial Audio

Each temple receives an ambient soundscape.

Examples:

- Temple bells
- Ambient environment
- Wind
- Distant traditional instruments

Keep this lightweight for the MVP.

---

### F9 — Kannada + English

Information panels should support:

**English**

and optionally:

**Kannada**

Example:

```text
PILLAR
ಕಂಬ
```

This makes the project specifically relevant to Karnataka.

---

### F10 — Educational Quiz

At the end of each temple:

### Test Your Knowledge

3–5 questions.

Example:

> Which architectural tradition is strongly associated with Belur and Halebidu?

The system displays:

```text
Score: 4 / 5
```

This is optional for the first prototype but provides an educational component.

---

## 8. Technical Architecture

### Recommended Stack

- **Unity + URP**
- **OpenXR**
- **XR Interaction Toolkit**
- **C#**
- **Blender**
- **JSON / ScriptableObjects**

### Architecture

```text
             VIRTUAL HERITAGE KARNATAKA
                         |
          ┌──────────────┴──────────────┐
          ↓                             ↓
    Temple Database                VR Engine
          |                             |
   ┌──────┼──────┐              ┌──────┼──────┐
   ↓      ↓      ↓              ↓      ↓      ↓
Belur  Halebidu Hampi       Movement Interaction Rendering
                              |        |          |
                              ↓        ↓          ↓
                           Teleport  Inspect    Lighting
                                     Objects     Materials
                                      |
                                      ↓
                                  Metadata
```

---

## 9. Data Structure

Each interactive architectural element can contain:

```json
{
  "name": "Lathe-Turned Pillar",
  "temple": "Hoysaleswara Temple",
  "period": "Hoysala",
  "material": "Stone",
  "description": "Decorative architectural pillar...",
  "historicalConfidence": "Documented"
}
```

This allows the same interaction system to work across every temple.

---

## 10. Computer Graphics Components

The project should explicitly demonstrate:

### 1. 3D Modeling
Temple structures and architectural elements.

### 2. Transformations
Translation, rotation, and scaling during object inspection.

### 3. Materials
Different stone/material appearances.

### 4. Lighting
Directional and ambient lighting.

### 5. Shadows
Real-time scene shadows.

### 6. Camera
VR stereoscopic camera system.

### 7. Rendering
Real-time rendering through Unity URP.

### 8. Wireframe Rendering
Optional technical visualization of meshes.

### 9. Animation
Temple transitions and Then/Now visualization.

---

## 11. VR Components

The VR implementation demonstrates:

- Head tracking
- Controller tracking
- Teleportation
- Ray interaction
- Object selection
- Object manipulation
- Spatial UI
- Immersive audio

---

## 12. Performance Requirements

Do not create unnecessarily high-poly temple models.

Target:

- Real-time VR rendering
- Stable frame rate
- Baked lighting wherever possible
- LOD for complex models
- Optimized textures
- Occlusion culling where useful
- Limited real-time lights

---

## 13. 7-Hour MVP Plan

### Hour 1 — Environment

- Unity project
- URP
- OpenXR
- XR Interaction Toolkit
- VR camera
- Basic scene

### Hour 2 — Temple 1

Create/import the first temple environment.

Focus on **one visually recognizable section**, not the entire temple.

### Hour 3 — Temple 2 + 3

Create simplified environments or scene variations.

### Hour 4 — Interaction

Implement:

- Teleportation
- Ray interaction
- Object selection
- Highlighting
- Inspection

### Hour 5 — Information System

Implement:

- Temple information
- Architectural metadata
- Information panels
- Timeline

### Hour 6 — Then/Now + Audio

Add:

- Then/Now toggle
- Transition
- Ambient audio
- Basic lighting polish

### Hour 7 — Polish + Demo

- UI
- Performance optimization
- Fix interaction bugs
- Add title screen
- Record demonstration
- Prepare presentation

---

## 14. Features Explicitly Out of Scope for MVP

Do **not** attempt these in the first 7-hour version:

- Complete archaeological reconstruction of every temple
- Full photogrammetry pipeline
- Multiplayer VR
- AI-generated historical reconstruction
- Scholar collaboration portal
- Cloud backend
- User accounts
- Full WebXR support
- Haptic-material simulation
- Foveated rendering
- 50+ temples

These can be presented as future work.

---

## 15. Future Scope

### Phase 2

- More Karnataka temples
- Higher-quality photogrammetry
- Better reconstruction
- Kannada narration
- More detailed architectural annotations

### Phase 3

- Historical reconstruction with confidence visualization
- Scholar annotations
- Community contributions
- Web-based companion application

### Phase 4

- AI-powered heritage guide
- Personalized learning
- Automated architectural-element recognition
- Multi-user VR heritage tours

---

## 16. Success Criteria

The prototype is considered successful if a user can:

1. Launch the application.
2. Select a Karnataka temple.
3. Enter the VR environment.
4. Navigate using teleportation.
5. Identify an interactive architectural element.
6. Select and inspect it.
7. Read its historical/architectural information.
8. Switch between Then and Now where supported.
9. Return to the temple hub.
10. Explore another temple.

---

## 17. Final Project Pitch

> **Virtual Heritage Karnataka** is an immersive VR platform for exploring and understanding Karnataka's historic temple architecture through interactive 3D reconstruction. Users can virtually visit selected temples, navigate their architectural spaces, inspect pillars and sculptures, access contextual historical information, and experience simplified Then-versus-Now visualizations. By combining real-time computer graphics, VR interaction, semantic architectural annotation, spatial audio, and digital heritage preservation, the project transforms traditional heritage documentation into an interactive educational experience.

---

## 18. Core Design Principle

> **Build a small VR experience where every interaction demonstrates a meaningful Computer Graphics or Virtual Reality concept.**

The goal is not to build the largest possible heritage platform in seven hours. The goal is to produce a **polished, technically demonstrable, and academically defensible VR prototype**.
