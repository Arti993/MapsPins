using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using DG.Tweening;
using Newtonsoft.Json;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(BoxCollider2D))]
public class PinMarker : MonoBehaviour, IPointerClickHandler, IDragHandler, IEndDragHandler
{
   [Header("Pin Panel Settings")]
    [SerializeField] private GameObject pinPanelPrefab; // Префаб панели маркера
    [SerializeField] private Vector2 panelOffset = new Vector2(150, 0); // Смещение панели от маркера
    
    private MapController mapController;
    private RectTransform rectTransform;
    private Canvas mainCanvas;
    
    // Состояние маркера
    private GameObject currentPinPanel;
    private bool isEditMode;
    
    // Флаг для отслеживания перетаскивания
    private bool wasDragged = false;
    
    public PinData PinData {get; private set;}
    
    // События для связи с MapController
    public event Action<string, string> OnPinDataSaved;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        mainCanvas = GetComponentInParent<Canvas>();
    }
    
    /// <summary>
    /// Инициализирует маркер с данными
    /// </summary>
    /// <param name="data">Данные маркера</param>
    /// <param name="editMode">true для режима редактирования, false для просмотра</param>
    public void Initialize(PinData data, bool editMode)
    {
        PinData = data;
        isEditMode = editMode;
        
        // В режиме редактирования панель показывается сразу для настройки
        // В режиме просмотра панель показывается сразу если есть имя
        if (editMode || (!editMode && PinData.name != ""))
        {
            ShowPinPanel();
        }
    }

    public void UpdatePinData(PinData pinData)
    {
        PinData = pinData;
    }
    
    private void ShowPinPanel()
    {
        if (pinPanelPrefab == null)
        {
            Debug.LogError("PinPanel prefab not assigned!");
            return;
        }
        
        // Используем PanelManager для закрытия всех открытых панелей
        SinglePanelVision.CloseAllOpenPanels();
        
        // Находим основной Canvas для размещения UI элементов
        Canvas mainCanvas = FindMainCanvas();
        if (mainCanvas == null)
        {
            Debug.LogError("Main Canvas not found! Please ensure there's a Canvas in the scene.");
            return;
        }
        
        // Создаем панель как дочерний объект Canvas (не карты)
        currentPinPanel = Instantiate(pinPanelPrefab, mainCanvas.transform);
        currentPinPanel.name = "PinPanel_" + gameObject.name;
        
        // Получаем компонент PinPanel
        PinPanel pinPanel = currentPinPanel.GetComponent<PinPanel>();
        if (pinPanel != null)
        {
            // Настраиваем панель
            pinPanel.Setup(this, PinData, isEditMode);
        }
        else
        {
            Debug.LogError("PinPanel component not found on prefab!");
            Destroy(currentPinPanel);
            currentPinPanel = null;
            return;
        }
        
        // Размещаем панель рядом с маркером в экранных координатах
        PositionPinPanelOnScreen();
        
        // Регистрируем панель в PanelManager
        SinglePanelVision.RegisterOpenPanel(this);
        
        // Добавляем плавное появление панели
        AnimatePanelAppearance();
        
        Debug.Log($"Pin panel shown for marker: {PinData.name}");
    }
    
    /// <summary>
    /// Анимирует плавное появление панели
    /// </summary>
    private void AnimatePanelAppearance()
    {
        if (currentPinPanel == null) return;
        
        // Получаем Canvas Group для анимации прозрачности
        CanvasGroup canvasGroup = currentPinPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = currentPinPanel.AddComponent<CanvasGroup>();
        }
        
        // Начинаем с прозрачного состояния
        canvasGroup.alpha = 0f;
        
        // Плавно появляемся
        canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad);
    }
    
    /// <summary>
    /// Анимирует плавное исчезновение панели
    /// </summary>
    private void AnimatePanelDisappearance(System.Action onComplete = null)
    {
        if (currentPinPanel == null)
        {
            onComplete?.Invoke();
            return;
        }
        
        // Получаем Canvas Group для анимации прозрачности
        CanvasGroup canvasGroup = currentPinPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = currentPinPanel.AddComponent<CanvasGroup>();
        }
        
        // Плавно исчезаем
        canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            onComplete?.Invoke();
        });
    }
    
    /// <summary>
    /// Находит основной Canvas в сцене для размещения UI элементов
    /// </summary>
    private Canvas FindMainCanvas()
    {
        // Ищем Canvas с компонентом Canvas
        Canvas[] canvases = GameObject.FindObjectsOfType<Canvas>();
        
        foreach (Canvas canvas in canvases)
        {
            // Предпочитаем Screen Space - Overlay Canvas
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvas;
            }
        }
        
        // Если не нашли Overlay, возвращаем первый попавшийся
        if (canvases.Length > 0)
        {
            return canvases[0];
        }
        
        return null;
    }
    
    /// <summary>
    /// Размещает панель в экранных координатах рядом с маркером
    /// </summary>
    private void PositionPinPanelOnScreen()
    {
        if (currentPinPanel == null) return;
        
        RectTransform markerRect = GetComponent<RectTransform>();
        RectTransform panelRect = currentPinPanel.GetComponent<RectTransform>();
        Canvas canvas = currentPinPanel.GetComponentInParent<Canvas>();
        
        if (markerRect != null && panelRect != null && canvas != null)
        {
            // Получаем позицию маркера в мировых координатах
            Vector3 markerWorldPos = markerRect.position;
            
            // Получаем позицию маркера в экранных координатах
            Vector2 markerScreenPos;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Для Overlay Canvas позиция в мировых координатах = экранные координаты
                markerScreenPos = markerWorldPos;
            }
            else
            {
                // Для других режимов Canvas используем Camera.main для конвертации
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    markerScreenPos = mainCamera.WorldToScreenPoint(markerWorldPos);
                }
                else
                {
                    // Запасной вариант - используем позицию как есть
                    markerScreenPos = markerWorldPos;
                }
            }
            
            // Размещаем панель с учетом смещения в экранных координатах
            Vector2 panelScreenPos = markerScreenPos + panelOffset;
            
            // Устанавливаем позицию панели (работает с любым Canvas mode)
            panelRect.position = new Vector3(panelScreenPos.x, panelScreenPos.y, 0);
        }
    }
    
    private void PositionPinPanel()
    {
        if (currentPinPanel == null) return;
        
        RectTransform markerRect = GetComponent<RectTransform>();
        RectTransform panelRect = currentPinPanel.GetComponent<RectTransform>();
        
        if (markerRect != null && panelRect != null)
        {
            // Размещаем панель с учетом смещения
            Vector3 markerWorldPos = markerRect.position;
            panelRect.position = markerWorldPos + new Vector3(panelOffset.x, panelOffset.y, 0);
        }
    }
    
    // Публичный метод для закрытия панели (может вызываться из PinPanel)
    public void ClosePinPanel()
    {
        if (currentPinPanel != null)
        {
            // Разрегистрируем панель из PanelManager
            SinglePanelVision.UnregisterOpenPanel(this);
            
            // Анимируем исчезновение, затем уничтожаем
            AnimatePanelDisappearance(() =>
            {
                Destroy(currentPinPanel);
                currentPinPanel = null;
            });
        }
    }
    
    // Публичный метод для обновления данных маркера (может вызываться из PinPanel)
    public void UpdatePinData(string name, string description, Texture2D image)
    {
        if (PinData != null)
        {
            bool positionChanged = false;
            bool dataChanged = false;
            
            // Запоминаем старую позицию для сравнения
            Vector2 oldPosition = PinData.mapPosition;
            
            // Проверяем изменились ли данные
            if (PinData.name != name || PinData.description != description || PinData.image != image)
            {
                dataChanged = true;
            }
            
            PinData.name = name;
            PinData.description = description;
            PinData.image = image;
            
            // Проверяем, изменилась ли позиция
            if (Vector2.Distance(oldPosition, PinData.mapPosition) > 0.01f)
            {
                positionChanged = true;
            }
            
            Debug.Log($"Pin data updated: name='{PinData.name}', description='{PinData.description}', position={PinData.mapPosition}");
            
            // Обновляем данные в MapController если позиция изменилась
            if (positionChanged && mapController != null)
            {
                mapController.UpdatePinPosition(gameObject, PinData.mapPosition);
            }
        }
    }
    
    // Публичный метод для установки MapController
    public void SetMapController(MapController controller)
    {
        mapController = controller;
        Debug.Log($"MapController set for PinMarker: {gameObject.name}");
    }
    
    // Публичный метод для сохранения данных и вызова события (может вызываться из PinPanel)
    public void SavePinData(string name, string description)
    {
        // Обновляем данные
        UpdatePinData(name, description, PinData.image);
        
        // Вызываем событие сохранения
        OnPinDataSaved?.Invoke(name, description);
        
        // Обновляем данные в MapController для синхронизации
        if (mapController != null)
        {
            mapController.UpdatePinData(gameObject, name, description, PinData.image);
        }
    }
    
    // Публичный метод для загрузки изображения (может вызываться из PinPanel)
    public void LoadPinImage()
    {
        // В Unity WebGL ограничены возможности работы с файловой системой
        // В данном примере используем заглушку
        
        // Создаём простую текстуру для демонстрации
        Texture2D demoTexture = new Texture2D(100, 100, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[100 * 100];
        
        // Создаём простой узор
        for (int y = 0; y < 100; y++)
        {
            for (int x = 0; x < 100; x++)
            {
                if ((x + y) % 20 < 10)
                {
                    pixels[y * 100 + x] = Color.blue;
                }
                else
                {
                    pixels[y * 100 + x] = Color.yellow;
                }
            }
        }
        
        demoTexture.SetPixels(pixels);
        demoTexture.Apply();
        
        // Обновляем данные маркера
        if (PinData != null)
        {
            PinData.image = demoTexture;
            Debug.Log("Demo image loaded for pin (File browser simulation)");
            
            // Обновляем отображение в панели, если она открыта
            if (currentPinPanel != null)
            {
                PinPanel pinPanel = currentPinPanel.GetComponent<PinPanel>();
                if (pinPanel != null)
                {
                    pinPanel.RefreshImage(PinData);
                }
            }
        }
    }
    
    // Публичный метод для получения данных маркера
    public PinData GetPinData()
    {
        return PinData;
    }
    
    // Публичный метод для проверки, открыта ли панель
    public bool IsPanelOpen()
    {
        return SinglePanelVision.IsPanelOpen(this) && currentPinPanel != null;
    }
    
    private void OnDestroy()
    {
        // Разрегистрируем панель из PanelManager при уничтожении объекта
        SinglePanelVision.UnregisterOpenPanel(this);
        
        // Очистка при уничтожении
        if (currentPinPanel != null)
        {
            Destroy(currentPinPanel);
        }
    }

    // IPointerClickHandler - обработчик клика
    public void OnPointerClick(PointerEventData eventData)
    {
        // Если был выполнен перетаскивание, не открываем панель
        if (wasDragged)
        {
            wasDragged = false; // Сбрасываем флаг после обработки
            return;
        }
        
        // Если панель уже открыта, закрываем её
        if (currentPinPanel != null)
        {
            ClosePinPanel();
            return;
        }
        
        // Иначе показываем панель
        ShowPinPanel();
    }

    // IDragHandler - обработчик перетаскивания
    public void OnDrag(PointerEventData eventData)
    {
        wasDragged = true; // Отмечаем, что началось перетаскивание
        rectTransform.anchoredPosition += eventData.delta / mapController.CurrentZoom / mainCanvas.scaleFactor;
    }

    // IEndDragHandler - обработчик завершения перетаскивания
    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"Pin drag ended. Final position: {rectTransform.anchoredPosition}");
        
        if (PinData != null)
        {
            PinData.mapPosition = rectTransform.anchoredPosition;
        }
    
        // Финальное обновление в MapController
        if (mapController != null)
        {
            mapController.UpdatePinPosition(gameObject, rectTransform.anchoredPosition);
        }
        
        // Сбрасываем флаг перетаскивания после завершения
        wasDragged = false;
    }
}