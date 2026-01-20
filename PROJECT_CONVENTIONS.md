# UnityTools Project Conventions

## Documentation Standards

### 1. Alphabetical Ordering
**CRITICAL**: All tool listings must be in **alphabetical order** by tool name.

This applies to:
- `README.md` - Advanced Tools section
- `GameDevToolsWelcomeWindow.cs` - Tool list in OnGUI()

**Example Order**:
1. Advanced Inspector
2. Asset Sync Tool
3. Feature Aggregator
4. Hidden Dependency Detector
5. Integrated Terminal
... (and so on alphabetically)

### 2. Menu Path Convention
All tools should use the menu path: `Tools/GameDevTools/[Tool Name]`

**Example**:
```csharp
[MenuItem("Tools/GameDevTools/Feature Aggregator")]
```

### 3. README.md Structure
When adding a new tool to README:
- Add it under `### 🔍 Advanced Tools` section
- Insert it in **alphabetical order**
- Use this format:
```markdown
- **Tool Name**:
    - **Feature 1**: Description
    - **Feature 2**: Description
    - **Access**: `Tools > GameDevTools > Tool Name`
```

### 4. Welcome Window Structure
When adding a new tool to `GameDevToolsWelcomeWindow.cs`:
- Add it in **alphabetical order** within the `OnGUI()` method
- Update the comment number (e.g., `// 3. Feature Aggregator`)
- Use this format:
```csharp
// X. Tool Name
DrawToolItem(
    "Tool Name",
    "Brief description of the tool.",
    "Tools/GameDevTools/Tool Name"  // or null for passive tools
);
```

### 5. Context Menu Conventions
For context menu items:
- Use `Assets/` prefix for Project window context menus
- Use descriptive names that indicate the action
- Example: `Assets/Feature Aggregator/Add Selected to Feature...`

## Code Standards

### 1. Namespace Convention
All tools should use the appropriate namespace:
- Editor tools: Use descriptive namespaces (e.g., `FeatureAggregator`, `UnityProductivityTools`)
- Keep namespaces consistent within a tool

### 2. EditorWindow Naming
- Window class: `[ToolName]Window` (e.g., `FeatureAggregatorWindow`)
- Menu item method: `ShowWindow()`

### 3. ScriptableObject Data
- Store tool data in `Editor/[ToolName]/` folder
- Use descriptive asset names
- Include `CreateAssetMenu` attribute with proper menu path

## File Organization

```
Editor/
├── [ToolName]/
│   ├── [ToolName]Window.cs
│   ├── [ToolName]Manager.cs
│   ├── [ToolName]Data.cs (ScriptableObject)
│   └── Utils/
│       └── Helper scripts
```

## Checklist for Adding New Tools

- [ ] Create tool in `Editor/[ToolName]/` folder
- [ ] Add MenuItem with path `Tools/GameDevTools/[Tool Name]`
- [ ] Update `README.md` in **alphabetical order**
- [ ] Update `GameDevToolsWelcomeWindow.cs` in **alphabetical order**
- [ ] Test tool functionality
- [ ] Verify menu path works
- [ ] Check that tool appears in Welcome Window

## Common Mistakes to Avoid

1. ❌ Adding tools to the end of lists instead of alphabetically
2. ❌ Using inconsistent menu paths (not using `Tools/GameDevTools/`)
3. ❌ Forgetting to update both README and Welcome Window
4. ❌ Using incorrect numbering in Welcome Window comments
5. ❌ Not testing the "Open Tool" button in Welcome Window

## Quick Reference

**Alphabetical Tool Order** (as of current version):
1. Add Scene to Build
2. Advanced Inspector
3. Asset Sync Tool
4. Feature Aggregator
5. Hidden Dependency Detector
6. Hierarchy Icons
7. Integrated Terminal
8. Note Dashboard
9. Object Comparison
10. Object Grouper
11. Project Bootstrapper
12. Quick Prefab Creator
13. Snapshot Manager
14. Task Manager
15. TODO Scanner
