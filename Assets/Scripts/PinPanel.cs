using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PinPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] TMP_InputField _nameInput;
    [SerializeField]private TMP_InputField _descriptionInput;
    [SerializeField]private Image _pinImage;
    [SerializeField]private Button _saveButton;
    [SerializeField]private Button _closeButton;
    [SerializeField]private Text _titleText;
    [SerializeField]private Text _imageHintText;
    
    private PinMarker _pinMarker;
    private PinData _pinData;
    private bool _isEditMode = true;
    
    private void Awake()
    {
        // Настраиваем обработчики событий
        if (_saveButton != null)
        {
            _saveButton.onClick.AddListener(OnSaveClicked);
        }
        
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(OnCloseClicked);
        }
        
        if (_pinImage != null && _isEditMode)
        {
            Button imageButton = _pinImage.GetComponent<Button>();
            if (imageButton == null)
            {
                imageButton = _pinImage.gameObject.AddComponent<Button>();
            }
            imageButton.onClick.AddListener(OnImageClicked);
        }
    }
    
    public void Setup(PinMarker pinMarker, PinData pinData, bool editMode)
    {
        _pinMarker = pinMarker;
        _pinData = pinData;
        _isEditMode = editMode;
        
        if (_titleText != null)
        {
            _titleText.text = editMode ? "Новый маркер" : "Маркер";
        }
        
        // Настраиваем интерактивность
        if (_nameInput != null)
        {
            _nameInput.interactable = editMode;
            _nameInput.text = pinData.name;
        }
        
        if (_descriptionInput != null)
        {
            _descriptionInput.interactable = editMode;
            _descriptionInput.text = pinData.description;
        }
        
        // Настраиваем изображение
        UpdateImageDisplay();
        
        // Показываем/скрываем кнопку сохранения
        if (_saveButton != null)
        {
            _saveButton.gameObject.SetActive(editMode);
        }
    }
    
    // Обновляет отображение изображения
    public void RefreshImage(PinData pinData)
    {
        this._pinData = pinData;
        UpdateImageDisplay();
    }
    
    private void UpdateImageDisplay()
    {
        if (_pinImage != null)
        {
            if (_pinData.image != null)
            {
                Sprite sprite = Sprite.Create(_pinData.image, new Rect(0, 0, _pinData.image.width, _pinData.image.height), new Vector2(0.5f, 0.5f));
                _pinImage.sprite = sprite;
                _pinImage.color = Color.white;
                if (_imageHintText != null)
                {
                    _imageHintText.text = "Изображение загружено";
                }
            }
            else
            {
                _pinImage.sprite = null;
                _pinImage.color = Color.gray;
                if (_imageHintText != null)
                {
                    _imageHintText.text = _isEditMode ? "Кликните для выбора изображения" : "Изображение отсутствует";
                }
            }
        }
    }
    
    private void OnSaveClicked()
    {
        if (_pinMarker != null && _pinData != null && _isEditMode)
        {
            // Получаем данные из полей ввода
            string name = _nameInput != null ? _nameInput.text : "";
            string description = _descriptionInput != null ? _descriptionInput.text : "";
            
            // Сохраняем данные через PinMarker (это вызовет событие)
            _pinMarker.SavePinData(name, description);
            
            // Закрываем панель через PinMarker
            _pinMarker.ClosePinPanel();
        }
    }
    
    private void OnCloseClicked()
    {
        if (_pinMarker != null)
        {
            _pinMarker.ClosePinPanel();
        }
    }
    
    private void OnImageClicked()
    {
        if (_pinMarker != null && _isEditMode)
        {
            _pinMarker.LoadPinImage();
        }
    }
}