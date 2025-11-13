using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PinPanel : MonoBehaviour
{
   [Header("UI Elements")]
    public TMP_InputField nameInput;
    public TMP_InputField descriptionInput;
    public Image pinImage;
    public Button saveButton;
    public Button closeButton;
    public Text titleText;
    public Text imageHintText;
    
    private PinMarker pinMarker;
    private PinData pinData;
    private bool isEditMode = true;
    
    private void Awake()
    {
        // Настраиваем обработчики событий
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveClicked);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
        
        if (pinImage != null && isEditMode)
        {
            Button imageButton = pinImage.GetComponent<Button>();
            if (imageButton == null)
            {
                imageButton = pinImage.gameObject.AddComponent<Button>();
            }
            imageButton.onClick.AddListener(OnImageClicked);
        }
    }
    
    public void Setup(PinMarker pinMarker, PinData pinData, bool editMode)
    {
        this.pinMarker = pinMarker;
        this.pinData = pinData;
        this.isEditMode = editMode;
        
        // Обновляем заголовок
        if (titleText != null)
        {
            titleText.text = editMode ? "Новый маркер" : "Маркер";
        }
        
        // Настраиваем интерактивность
        if (nameInput != null)
        {
            nameInput.interactable = editMode;
            nameInput.text = pinData.name;
        }
        
        if (descriptionInput != null)
        {
            descriptionInput.interactable = editMode;
            descriptionInput.text = pinData.description;
        }
        
        // Настраиваем изображение
        UpdateImageDisplay();
        
        // Показываем/скрываем кнопку сохранения
        if (saveButton != null)
        {
            saveButton.gameObject.SetActive(editMode);
        }
    }
    
    // Обновляет отображение изображения
    public void RefreshImage(PinData pinData)
    {
        this.pinData = pinData;
        UpdateImageDisplay();
    }
    
    private void UpdateImageDisplay()
    {
        if (pinImage != null)
        {
            if (pinData.image != null)
            {
                Sprite sprite = Sprite.Create(pinData.image, new Rect(0, 0, pinData.image.width, pinData.image.height), new Vector2(0.5f, 0.5f));
                pinImage.sprite = sprite;
                pinImage.color = Color.white;
                if (imageHintText != null)
                {
                    imageHintText.text = "Изображение загружено";
                }
            }
            else
            {
                pinImage.sprite = null;
                pinImage.color = Color.gray;
                if (imageHintText != null)
                {
                    imageHintText.text = isEditMode ? "Кликните для выбора изображения" : "Изображение отсутствует";
                }
            }
        }
    }
    
    private void OnSaveClicked()
    {
        if (pinMarker != null && pinData != null && isEditMode)
        {
            // Получаем данные из полей ввода
            string name = nameInput != null ? nameInput.text : "";
            string description = descriptionInput != null ? descriptionInput.text : "";
            
            // Сохраняем данные через PinMarker (это вызовет событие)
            pinMarker.SavePinData(name, description);
            
            // Закрываем панель через PinMarker
            pinMarker.ClosePinPanel();
        }
    }
    
    private void OnCloseClicked()
    {
        if (pinMarker != null)
        {
            pinMarker.ClosePinPanel();
        }
    }
    
    private void OnImageClicked()
    {
        if (pinMarker != null && isEditMode)
        {
            pinMarker.LoadPinImage();
        }
    }
}