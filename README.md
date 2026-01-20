# Unity Productivity Tools

A comprehensive collection of Unity Editor tools designed to enhance productivity and streamline your development workflow.

## Features

### � Workflow & Productivity
- **1.1 Feature Aggregator**:
    - **Feature Organization**: Group related scripts and assets by feature/concept for better project organization.
    - **One-Click Access**: Open all scripts in a feature with a single click.
    - **Drag & Drop**: Easily add scripts and assets to features via drag and drop.
    - **Context Menu**: Right-click any asset to add it to a feature.
    - **Search & Filter**: Quickly find features with the built-in search bar.
    - **Access**: `Tools > GameDevTools > Feature Aggregator`

- **1.2 Project Bootstrapper**:
    - **Quick Setup**: Initialize empty projects with a standard folder structure (_Project, Scripts, Art, etc.).
    - **Base Scripts**: Generate essential helper scripts like Singleton<T>, ObjectPool<T>, GameConstants, and more.
    - **Scene Setup**: Auto-create Boot, Menu, and Gameplay scenes and add them to Build Settings.
    - **Standard Settings**: One-click application of recommended project settings (Linear color space, etc.).

- **1.3 Task Manager**:
    - **Global To-Do List**: Manage tasks project-wide without treating them as MonoBehaviours.
    - **Features**: Priority levels (color-coded), status tracking, and owner assignment.
    - **Deep Linking**: Link tasks to GameObjects or Project Assets for quick context.
    - **Persistence**: Tasks are saved to a specific asset file and persist across sessions.

- **1.4 Task Manager (Synced)**:
    - **Real-Time Sync**: Synchronize tasks with connected clients via WebSockets.
    - **Team Sync**: Share task lists with team members.
    - **Access**: `Tools > GameDevTools > Task Manager (Synced)`

- **1.5 TODO/FIXME Scanner**:
    - **Code Scanning**: Automatically finds `//TODO` and `//FIXME` comments in all your project scripts.
    - **Navigation**: Click on any item to open the file directly in your IDE at the correct line.

### 🏗️ Scene & Hierarchy
- **2.1 Add Scene to Build**: Quickly add the current scene to the Build Settings. (Right-click on Scene Asset > Add to Build Settings)

- **2.2 Hierarchy Icons**: 
    - **Visual Indicators**: Indicators in the Hierarchy view for better object identification.
    - **Dependency Indicators**: Visual cues for object dependencies within the project.

- **2.3 Object Comparison Tool**:
    - **Deep Comparison**: Compare two GameObjects, their Transforms, Components, and Hierarchy.
    - **Interactive Syncing**: Selectively apply changes between objects with one-click syncing (A → B or B → A).
    - **Visual History**: Track all changes made during a session with a built-in audit trail.
    - **Deferred Processing**: High stability IMGUI implementation prevents layout errors and flickering.

- **2.4 Object Grouper**:
    - **Non-Destructive Grouping**: Organize objects into logical groups without changing the Hierarchy.
    - **Visual Indicators**: Colored dots in Hierarchy for grouped objects.
    - **Bulk Operations**: Toggle visibility, lock state, and selection for entire groups.
    - **Drag & Drop**: Easily add objects to groups via drag and drop.

- **2.5 Snapshot Manager**: 
    - **Capture**: specific states of GameObjects.
    - **Restore**: Revert objects to saved states.
    - **Undo Support**: Full undo/redo for all state restorations.

### � Asset Utilities
- **3.1 Advanced Inspector**:
    - **Favorites System**: Pin frequently used components or properties for quick access.
    - **Component Search**: Instantly filter components on a GameObject by name.
    - **Preset Manager**: Save and load component configurations as JSON files.
    - **Bulk Editing**: Edit components across multiple GameObjects simultaneously with a unified interface.
    - **Script Editor**: Edit MonoBehaviour scripts directly from the inspector with inline or maximized window modes.

- **3.2 Asset Sync Tool**:
    - **Sync**: Mark files/folders to automatically sync to an external location (backup/shared drive).
    - **History**: Track sync operations with a built-in log.
    - **Smart UI**: Hover highlights and quick navigation to external folders.
    - **Context Menu**: Right-click assets to "Mark for Sync".

- **3.3 Hidden Dependency Detector**: Analyze and find hidden dependencies in your project assets (e.g., shaders, materials) to optimize build size and track references.

- **3.4 Integrated Terminal**:
    - **Multi-Tabbed**: Support for multiple terminal sessions within the same window.
    - **Shell Support**: Switch between CMD and PowerShell on Windows.
    - **Command History**: Navigate through previous commands with Up/Down arrows.
    - **Context Menu**: Right-click any folder or asset to "Open in Terminal Here".
    - **Monospace Font**: Clean, readable output using monospace typography.

- **3.5 Note Dashboard**: 
    - **Note Component**: Add documentation and comments directly to your GameObjects. Visible in the Scene view with customizable icons and colors.
    - **Centralized View**: A centralized window (`Tools > GameDevTools > Note Dashboard`) to view, search, and manage all notes in your scene.

- **3.6 Quick Prefab Creator**: Create prefabs from selected objects instantly. (Right-click > Prefab > Make Prefab)


### 🛠 Toolbar Extender (Core Feature)
Enhanced toolbar with quick access to common operations:

- **Selection History**: Navigate back (`◀`) and forward (`▶`) through your object selection history.
- **Quick Shortcuts**: Instant access to Project Settings (`⚙`) and User Preferences (`⚙`).
- **Platform Switcher**: Quickly switch build targets (Windows, Android, iOS, WebGL) via a dropdown menu.
- **Scene Switcher**: Fast switching between scenes listed in your Build Settings.
- **Find Scene**: Locate the currently active scene asset in the Project view (`🔍`).





## Installation

1. Open Unity Package Manager
2. Click "+" and select "Add package from git URL..."
3. Paste the repository URL: `https://github.com/yourusername/unity-productivity-tools.git`

## Usage

Most tools are automatically active upon installation.
- Access **Toolbar** features directly from the top unity toolbar.
- **Snapshot Tool** and **Hidden Dependency Detector** can be found under the `Tools` menu (or their specific menu paths).
- Add the **Note** component to any GameObject via `AddComponent > Game Dev Tools > Note`.

## License

MIT License
MIT License
