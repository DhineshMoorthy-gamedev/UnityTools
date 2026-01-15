# Unity Productivity Tools

A comprehensive collection of Unity Editor tools designed to enhance productivity and streamline your development workflow.

## Features

### 🛠 Toolbar Extender
Enhanced toolbar with quick access to common operations:

- **Selection History**: Navigate back (`◀`) and forward (`▶`) through your object selection history.
- **Quick Shortcuts**: Instant access to Project Settings (`⚙`) and User Preferences (`⚙`).
- **Platform Switcher**: Quickly switch build targets (Windows, Android, iOS, WebGL) via a dropdown menu.
- **Scene Switcher**: Fast switching between scenes listed in your Build Settings.
- **Find Scene**: Locate the currently active scene asset in the Project view (`🔍`).

### 📝 Hierarchy Tools
- **Hierarchy Icons**: Visual indicators in the Hierarchy view for better object identification.
- **Dependency Indicators**: Visual cues for object dependencies within the project.

### ⚡ Productivity Utilities
- **Quick Prefab Creator**: Create prefabs from selected objects instantly.
- **Add Scene to Build**: Quickly add the current scene to the Build Settings.
- **Selection History**: Keeps track of your selected objects for easy navigation.

### 📦 Components
- **Note Component**: Add documentation and comments directly to your GameObjects. Visible in the Scene view with customizable icons and colors. Great for leaving reminders or team instructions.

### 🔍 Advanced Tools
- **Hidden Dependency Detector**: Analyze and find hidden dependencies in your project assets (e.g., shaders, materials) to optimize build size and track references.
- **Snapshot Tool**: 
    - **Capture**: specific states of GameObjects.
    - **Restore**: Revert objects to saved states.
    - Useful for testing different configurations without permanent changes.

- **Task Manager**:
    - **Global To-Do List**: Manage tasks project-wide without treating them as MonoBehaviours.
    - **Features**: Priority levels (color-coded), status tracking, and owner assignment.
    - **Persistence**: Tasks are saved to a specific asset file and persist across sessions.

- **Object Grouper**:
    - **Non-Destructive Grouping**: Organize objects into logical groups without changing the Hierarchy.
    - **Visual Indicators**: Colored dots in Hierarchy for grouped objects.
    - **Bulk Operations**: Toggle visibility, lock state, and selection for entire groups.
    - **Drag & Drop**: Easily add objects to groups via drag and drop.
    
- **Project Bootstrapper**:
    - **Quick Setup**: Initialize empty projects with a standard folder structure (_Project, Scripts, Art, etc.).
    - **Base Scripts**: Generate essential helper scripts like Singleton<T>, ObjectPool<T>, GameConstants, and more.
    - **Scene Setup**: Auto-create Boot, Menu, and Gameplay scenes and add them to Build Settings.
    - **Standard Settings**: One-click application of recommended project settings (Linear color space, etc.).

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
