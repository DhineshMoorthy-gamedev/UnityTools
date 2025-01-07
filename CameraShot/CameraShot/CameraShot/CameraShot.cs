using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class CameraShot : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    Button button1;
    Button button2;
    Toggle enableButtonToggle;
    TextField textField1;
    TabView tabView1;
    ObjectField objectField;

    [MenuItem("Window/UI Toolkit/CameraShot")]
    public static void ShowExample()
    {
        CameraShot wnd = GetWindow<CameraShot>();
        wnd.titleContent = new GUIContent("CameraShot");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Label
        VisualElement label = new Label("Hello World! From C#");
        root.Add(label);

        //label
        enableButtonToggle = new Toggle()
        {
            text = "enable button"
        };
        enableButtonToggle.value = true;
        //action
        enableButtonToggle.RegisterCallback<ChangeEvent<bool>>((evt) =>
        {
            //Debug.Log(enableButtonToggle.value);
            if (enableButtonToggle.value)
            {
                enableButtonToggle.text = "click again to disable";
                button1.SetEnabled(true);
                button1.style.opacity = 1;
            }
            else
            {
                enableButtonToggle.text = "click again to enable";
                button1.SetEnabled(false);
                button1.style.opacity = 0.5f;
            }
        });

        //root.Add(enableButtonToggle);

        // Button 1
        button1 = new Button(() =>
        {
            //ToggleButtonText(button1);
            //Debug.Log("hi from camera shot");
        })
        {
            text = "see console"
        };
        button1.style.color = Color.yellow;
        button1.style.width = 80;
        button1.style.height = 20;
        button1.RegisterCallback<ClickEvent>(OnBoxClicked);
        // Button 2
        button2 = new Button(() =>
        {
            //ToggleButtonText(button1);
            //Debug.Log("hi from camera shot");
        })
        {
            text = "see console"
        };
        button2.style.color = Color.yellow;
        button2.style.width = 80;
        button2.style.height = 20;
        button2.RegisterCallback<ClickEvent>(OnBoxClicked);
        //root.Add(button1);

        textField1 = new TextField("enter text to log in the console");
        //root.Add(textField1);

        tabView1 = new TabView();
        var tab = new Tab("Experimental tab");
        tab.Add(button1);
        button2.style.position = Position.Absolute;
        button2.style.left = 80;
        tab.Add(button2);
        tab.Add(textField1);
        tab.Add(enableButtonToggle);
        tabView1.Add(tab);
        var tab2 = new Tab("Camera Shot");
        objectField = new ObjectField("main camera");
        objectField.objectType = typeof(Camera);
        tab2.Add(objectField);
        tabView1.Add(tab2);
        root.Add(tabView1);

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset?.Instantiate();
        if (labelFromUXML != null)
        {
            root.Add(labelFromUXML);
        }

        Button result = root.Q<Button>(name:"one");
        result.RegisterCallback<ClickEvent>(OnBoxClicked);

    }
    private void OnBoxClicked(ClickEvent evt)
    {
        if (textField1.value != null)
        {
            Debug.Log(textField1.value);
        }
        if (objectField.value != null)
        {
            //Debug.Log(objectField.value.name);
            Camera cam = (Camera)objectField.value;
            Debug.Log(cam.transform.position);
        }
    }

    private void ToggleButtonText(Button button)
    {
        if (button != null)
        {
            //button.text = button.text.EndsWith("Button") ? "Button (Clicked)" : "Button";
            Debug.Log($"Button '{button.text}' was clicked.");
        }
    }
}
