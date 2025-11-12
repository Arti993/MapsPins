using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.IO;
using System;

[RequireComponent(typeof(Camera))]
public class MapController : MonoBehaviour
{
   [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Image mapImage;
    
    [Header("Pin Settings")]
    [SerializeField] private GameObject pinMarkerPrefab; // Префаб маркера с PinMarker скриптом
    [SerializeField] private float pinTolerance = 10f; // Допустимое движение мыши в пикселях

    [Header("Camera Settings")]
    [SerializeField] private Vector2 cameraScale = Vector2.one;
    [SerializeField] private float[] zoomSteps = { 1f, 1.5f, 2f, 3f };
    [SerializeField] private int currentZoomIndex = 0;

    [Header("Drag Settings")]
    [SerializeField] private float dragMultiplier = 1f;

    private PlayerInput playerInput;
    private Vector3 initialCameraPosition;
    private float initialOrthographicSize;
    private Vector2 mapSize;
    private Vector2 cameraBounds;
    private bool isDragging = false;
    private Vector3 lastMousePosition;
    
    // 🎯 НОВОЕ: храним начальный размер изображения карты
    private Vector2 initialMapSize;
    private Vector3 initialMapPosition;
    
    // 🎯 НОВОЕ: для UI drag
    private Vector3 lastMapPosition;
    private Vector2 canvasSize;
    
    // 📍 ПЕРЕМЕННЫЕ ДЛЯ СОЗДАНИЯ МАРКЕРОВ
    private Transform pinContainer; // Контейнер для маркеров (дочерний к карте)

    // 📋 СЛОВАРЬ ДАННЫХ МАРКЕРОВ
    private Dictionary<GameObject, PinMarker.PinData> pinDataDictionary = new Dictionary<GameObject, PinMarker.PinData>();
    
    public float CurrentZoom => zoomSteps[currentZoomIndex];

    private void Awake()
    {
        playerInput = new PlayerInput();
        
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();

        // Получаем размер карты
        if (mapImage != null)
        {
            var rectTransform = mapImage.rectTransform;
            mapSize = rectTransform.rect.size;
            initialMapSize = mapSize;
            initialMapPosition = rectTransform.localPosition;
        }
        
        // 🎯 НОВОЕ: получаем размер Canvas
        Canvas canvas = GetCanvas();
        if (canvas != null)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasSize = canvasRect.rect.size;
            }
        }
        
        // 📍 Создаём контейнер для маркеров (как дочерний к карте)
        CreatePinContainer();
    }
    
    private void Start()
    {
        // Сохраняем начальную позицию камеры и размер
        initialCameraPosition = transform.position;
        initialOrthographicSize = mainCamera.orthographicSize;
        
        // Устанавливаем камеру на максимальное отдаление (масштаб 1)
        SetCameraScale(zoomSteps[currentZoomIndex]);
    }
    
    private void OnEnable()
    {
        if (playerInput != null)
        {
            playerInput.Enable();
            
            // ⚡ ИСПРАВЛЕНО: используем только performed для прокрутки
            playerInput.UI.Zoom.performed += OnZoomPerformed;
            playerInput.UI.Drag.started += OnDragStarted;
            playerInput.UI.Drag.performed += OnDragPerformed;
            playerInput.UI.Drag.canceled += OnDragCanceled;
            
            // 📍 НОВОЕ: подписываемся на события создания маркеров
            playerInput.UI.MakePin.started += OnMakePinStarted;
            playerInput.UI.MakePin.performed += OnMakePinPerformed;
            playerInput.UI.MakePin.canceled += OnMakePinCanceled;
        }
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            playerInput.UI.Zoom.performed -= OnZoomPerformed;
            playerInput.UI.Drag.started -= OnDragStarted;
            playerInput.UI.Drag.performed -= OnDragPerformed;
            playerInput.UI.Drag.canceled -= OnDragCanceled;
            
            // 📍 НОВОЕ: отписываемся от событий создания маркеров
            playerInput.UI.MakePin.started -= OnMakePinStarted;
            playerInput.UI.MakePin.performed -= OnMakePinPerformed;
            playerInput.UI.MakePin.canceled -= OnMakePinCanceled;
            
            playerInput.Disable();
        }
    }

    private void OnZoomPerformed(InputAction.CallbackContext context)
    {
        Vector2 scrollValue = context.ReadValue<Vector2>();
        Debug.Log($"Zoom performed: {scrollValue}");
        
        // Проверяем, есть ли движение прокрутки
        if (Mathf.Abs(scrollValue.y) > 0.01f)
        {
            HandleZoom(scrollValue.y);
        }
    }

    private void OnDragStarted(InputAction.CallbackContext context)
    {
        isDragging = true;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        lastMousePosition = new Vector3(mousePos.x, mousePos.y, 0);
        
        // 🎯 НОВОЕ: сохраняем начальную позицию карты
        if (mapImage != null)
        {
            lastMapPosition = mapImage.rectTransform.localPosition;
        }
        
        Debug.Log("Drag started");
    }

    private void OnDragPerformed(InputAction.CallbackContext context)
    {
        if (!isDragging) return;
        
        // 🎯 ИСПРАВЛЕНО: drag работает при любом масштабе > 1
        if (cameraScale.x <= 1f) 
        {
            Debug.Log("Drag disabled at scale 1");
            return;
        }

        Vector2 currentMousePos = Mouse.current.position.ReadValue();
        Vector3 currentMousePosition = new Vector3(currentMousePos.x, currentMousePos.y, 0);
        
        // Проверка, что мышь в пределах экрана с небольшим буфером
        if (currentMousePosition.x < -10 || currentMousePosition.x > Screen.width + 10 ||
            currentMousePosition.y < -10 || currentMousePosition.y > Screen.height + 10)
        {
            lastMousePosition = currentMousePosition;
            return;
        }
        
        Vector3 mouseDelta = currentMousePosition - lastMousePosition;
        
        // 🎯 ИСПРАВЛЕНО: работаем с UI координатами, а не мировыми
        ApplyUIDrag(mouseDelta);
        
        lastMousePosition = currentMousePosition;
    }

    private void OnDragCanceled(InputAction.CallbackContext context)
    {
        isDragging = false;
        Debug.Log("Drag canceled");
    }
    
    // 📍 НОВЫЕ МЕТОДЫ ДЛЯ СОЗДАНИЯ МАРКЕРОВ
    
    private void OnMakePinStarted(InputAction.CallbackContext context)
    {
        // Создаём маркер сразу при нажатии
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        
        Debug.Log($"Pin created immediately at: {mousePosition}");
        CreatePinAtPosition(mousePosition);
    }
    
    private void OnMakePinPerformed(InputAction.CallbackContext context)
    {
        // Больше не используется - маркер создаётся в OnMakePinStarted
    }
    
    private void OnMakePinCanceled(InputAction.CallbackContext context)
    {
        // Больше не используется
    }
    
    // 🗺️ Создаёт маркер из префаба
    private void CreatePinAtPosition(Vector2 screenPosition)
    {
        if (mapImage == null)
        {
            Debug.LogError("Map image not found!");
            return;
        }
        
        // Проверяем наличие префаба маркера
        if (pinMarkerPrefab == null)
        {
            Debug.LogError("Pin marker prefab not assigned!");
            return;
        }
        
        // Убеждаемся, что контейнер существует и правильно настроен
        if (pinContainer == null)
        {
            CreatePinContainer();
        }
        
        if (pinContainer == null)
        {
            Debug.LogError("PinContainer not created!");
            return;
        }
        
        Debug.Log($"Creating pin with parent: {pinContainer.name}, map: {mapImage.name}");
        
        // 🗺️ КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: создаём маркер из префаба
        GameObject pinObject = Instantiate(pinMarkerPrefab, pinContainer);
        pinObject.name = $"Pin_{System.DateTime.Now.Ticks}";
        
        // 🗺️ КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: конвертируем экранные координаты в локальные координаты карты
        Vector2 mapLocalPosition = ConvertScreenToMapLocal(screenPosition);
        
        // Настраиваем RectTransform маркера
        RectTransform rectTransform = pinObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = mapLocalPosition;
            rectTransform.SetAsLastSibling(); // Поднимаем Z для отображения поверх карты
        }
        
        // Получаем PinMarker компонент и инициализируем его
        PinMarker pinMarker = pinObject.GetComponent<PinMarker>();
        if (pinMarker != null)
        {
            // Устанавливаем связь с MapController для перетаскивания
            pinMarker.SetMapController(this);
            
            // Создаём новые данные для маркера
            PinMarker.PinData newPinData = new PinMarker.PinData("", "", null, mapLocalPosition);
            pinDataDictionary[pinObject] = newPinData;
            
            // Подписываемся на событие сохранения данных маркера
            pinMarker.OnPinDataSaved += (name, description) => {
                // Обновляем данные в словаре при сохранении
                if (pinDataDictionary.ContainsKey(pinObject))
                {
                    pinDataDictionary[pinObject].name = name;
                    pinDataDictionary[pinObject].description = description;
                }
            };
            
            // Инициализируем маркер в режиме редактирования
            pinMarker.Initialize(newPinData, true);
        }
        else
        {
            Debug.LogError("PinMarker component not found on prefab!");
        }
        
        Debug.Log($"Pin created at map local position: {mapLocalPosition}");
    }
    
    // 🗺️ ИСПРАВЛЕНО: Создаёт контейнер для маркеров как дочерний к карте
    private void CreatePinContainer()
    {
        if (mapImage == null)
        {
            Debug.LogError("Map image not found for PinContainer!");
            return;
        }
        
        Debug.Log($"Creating PinContainer for map: {mapImage.name}");
        
        // Ищем существующий контейнер
        Transform existingContainer = mapImage.transform.Find("PinContainer");
        
        if (existingContainer != null)
        {
            pinContainer = existingContainer;
            Debug.Log("Using existing PinContainer");
        }
        else
        {
            // Создаём новый контейнер как дочерний к карте
            GameObject containerObject = new GameObject("PinContainer");
            pinContainer = containerObject.transform;
            pinContainer.SetParent(mapImage.transform, false);
            
            Debug.Log($"New PinContainer created as child of: {mapImage.name}");
            
            // Настраиваем RectTransform контейнера
            RectTransform containerRect = containerObject.GetComponent<RectTransform>();
            if (containerRect == null)
            {
                containerRect = containerObject.AddComponent<RectTransform>();
            }
            
            // Растягиваем контейнер на всю карту
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.sizeDelta = Vector2.zero;
            containerRect.anchoredPosition = Vector2.zero;
            
            // Устанавливаем сортировку
            containerRect.SetAsLastSibling(); // Контейнер маркеров поверх карты
        }
    }
    
    // 🗺️ Конвертирует экранные координаты в локальные координаты карты (ПРИВАТНЫЙ)
    private Vector2 ConvertScreenToMapLocal(Vector2 screenPosition)
    {
        if (mapImage == null)
        {
            Debug.LogError("Map image not found for coordinate conversion!");
            return screenPosition;
        }
        
        Canvas canvas = GetCanvas();
        if (canvas == null)
        {
            Debug.LogError("Canvas not found for coordinate conversion!");
            return screenPosition;
        }
        
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            Debug.LogError("Canvas RectTransform not found!");
            return screenPosition;
        }
        
        // Сначала конвертируем экранные координаты в координаты Canvas
        Vector2 canvasLocalPosition;
        
        // Определяем, какой метод конвертации использовать
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Для Screen Space Overlay
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, 
                screenPosition, 
                null, // Нет камеры для overlay
                out localPoint
            );
            canvasLocalPosition = localPoint;
        }
        else
        {
            // Для Screen Space Camera
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, 
                screenPosition, 
                canvas.worldCamera,
                out localPoint
            );
            canvasLocalPosition = localPoint;
        }
        
        // Затем конвертируем координаты Canvas в локальные координаты карты
        Vector2 mapLocalPosition = CanvasLocalToMapLocal(canvasLocalPosition);
        
        Debug.Log($"Screen position {screenPosition} -> Canvas {canvasLocalPosition} -> Map local {mapLocalPosition}");
        return mapLocalPosition;
    }
    
    // 🗺️ ПУБЛИЧНЫЙ МЕТОД: конвертация экранных координат в локальные координаты карты (ДЛЯ PIN MARKER)
    public Vector2 ScreenToMapLocalPosition(Vector2 screenPosition)
    {
        return ConvertScreenToMapLocal(screenPosition);
    }
    
    // 🗺️ Конвертирует координаты Canvas в локальные координаты карты
    private Vector2 CanvasLocalToMapLocal(Vector2 canvasLocalPosition)
    {
        if (mapImage == null)
        {
            Debug.LogError("Map image not found for canvas to map conversion!");
            return canvasLocalPosition;
        }
        
        RectTransform mapRect = mapImage.rectTransform;
        RectTransform canvasRect = mapRect.parent as RectTransform;
        
        if (canvasRect == null)
        {
            Debug.LogError("Canvas RectTransform not found!");
            return canvasLocalPosition;
        }
        
        // Получаем мировую позицию точки в Canvas
        Vector3 worldPoint = canvasRect.TransformPoint(canvasLocalPosition);
        
        // Конвертируем мировую позицию в локальную позицию карты
        Vector2 mapLocalPosition = mapRect.InverseTransformPoint(worldPoint);
        
        Debug.Log($"Canvas local {canvasLocalPosition} -> World {worldPoint} -> Map local {mapLocalPosition}");
        return mapLocalPosition;
    }
    
    // 🎯 НОВЫЙ МЕТОД: перемещение UI карты
    private void ApplyUIDrag(Vector3 mouseDelta)
    {
        if (mapImage == null) return;
        
        var rectTransform = mapImage.rectTransform;
        
        // Вычисляем движение с учетом текущего масштаба карты
        // Чем больше масштаб, тем меньше нужно двигать для компенсации
        float scaleFactor = 1f / cameraScale.x;
        
        Vector3 newPosition = lastMapPosition + mouseDelta * dragMultiplier * scaleFactor;
        
        // Применяем новую позицию
        rectTransform.localPosition = newPosition;
        
        // Обновляем запомненную позицию
        lastMapPosition = newPosition;
        
        // Ограничиваем движение в пределах экрана
        ClampMapPosition();
        
        Debug.Log($"Map moved to: {rectTransform.localPosition}");
    }
    
    // 🎯 НОВЫЙ МЕТОД: ограничение позиции карты
    private void ClampMapPosition()
    {
        if (mapImage == null) return;
        
        var rectTransform = mapImage.rectTransform;
        
        // Получаем текущие размеры карты с учетом масштаба
        float scaledMapWidth = initialMapSize.x * cameraScale.x;
        float scaledMapHeight = initialMapSize.y * cameraScale.y;
        
        // Вычисляем границы, чтобы карта не выходила за края экрана
        float halfScaledMapWidth = scaledMapWidth * 0.5f;
        float halfScaledMapHeight = scaledMapHeight * 0.5f;
        
        // Границы экрана
        float screenHalfWidth = canvasSize.x * 0.5f;
        float screenHalfHeight = canvasSize.y * 0.5f;
        
        // Если карта меньше экрана - центрируем её
        if (scaledMapWidth <= canvasSize.x && scaledMapHeight <= canvasSize.y)
        {
            rectTransform.localPosition = Vector3.zero;
            return;
        }
        
        // Ограничиваем позицию карты
        Vector3 currentPos = rectTransform.localPosition;
        currentPos.x = Mathf.Clamp(currentPos.x, -halfScaledMapWidth + screenHalfWidth, halfScaledMapWidth - screenHalfWidth);
        currentPos.y = Mathf.Clamp(currentPos.y, -halfScaledMapHeight + screenHalfHeight, halfScaledMapHeight - screenHalfHeight);
        currentPos.z = initialMapPosition.z; // Сохраняем Z координату
        
        rectTransform.localPosition = currentPos;
    }

    private void HandleZoom(float scrollValue)
    {
        Debug.Log($"HandleZoom called with: {scrollValue}");
        
        int newZoomIndex = currentZoomIndex;
        
        if (scrollValue > 0)
        {
            // Прокрутка вверх - приближение
            newZoomIndex = Mathf.Clamp(currentZoomIndex + 1, 0, zoomSteps.Length - 1);
            Debug.Log("Zooming in");
        }
        else if (scrollValue < 0)
        {
            // Прокрутка вниз - отдаление
            newZoomIndex = Mathf.Clamp(currentZoomIndex - 1, 0, zoomSteps.Length - 1);
            Debug.Log("Zooming out");
        }

        if (newZoomIndex != currentZoomIndex)
        {
            currentZoomIndex = newZoomIndex;
            Debug.Log($"Setting camera scale to: {zoomSteps[currentZoomIndex]}");
            SetCameraScale(zoomSteps[currentZoomIndex]);
        }
        else
        {
            Debug.Log("Zoom index unchanged");
        }
    }

    private void SetCameraScale(float scale)
    {
        cameraScale = Vector2.one * scale;
        
        // 🎯 ПРИМЕНЯЕМ МАСШТАБ К UI КАРТЕ
        ApplyMapScale();
        
        // Применяем масштаб к камере
        UpdateCameraProjection();
        
        // Получаем размер видимой области камеры в мировых координатах
        float worldHeight = mainCamera.orthographicSize * 2f;
        float worldWidth = worldHeight * mainCamera.aspect;
        
        cameraBounds = new Vector2(worldWidth, worldHeight);
        
        Debug.Log($"Camera orthographic size: {mainCamera.orthographicSize}");
        Debug.Log($"Map scale: {mapImage.rectTransform.localScale}");
        
        // Проверяем, что камера может показать всю карту при данном масштабе
        if (cameraScale.x <= 1f)
        {
            // При максимальном отдалении (масштаб 1) центрируем камеру и карту
            transform.position = new Vector3(initialCameraPosition.x, initialCameraPosition.y, initialCameraPosition.z);
            if (mapImage != null)
            {
                mapImage.rectTransform.localPosition = initialMapPosition;
            }
        }
        else
        {
            // При зуме ограничиваем позицию карты
            ClampMapPosition();
        }
    }

    // 🎯 НОВЫЙ МЕТОД: масштабируем UI карту
    private void ApplyMapScale()
    {
        if (mapImage == null) return;
        
        var rectTransform = mapImage.rectTransform;
        
        // Применяем масштаб к изображению карты
        rectTransform.localScale = new Vector3(cameraScale.x, cameraScale.y, 1f);
    }

    private void UpdateCameraProjection()
    {
        // Применяем масштаб к камере (исправленная формула)
        mainCamera.orthographicSize = initialOrthographicSize / cameraScale.y;
    }
    
    // 🎯 ВСПОМОГАТЕЛЬНЫЙ МЕТОД: получить Canvas
    private Canvas GetCanvas()
    {
        if (mapImage != null)
        {
            return mapImage.GetComponentInParent<Canvas>();
        }
        return null;
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: получение всех маркеров и их данных
    public Dictionary<GameObject, PinMarker.PinData> GetAllPinsData()
    {
        return pinDataDictionary;
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: удаление маркера по объекту
    public void RemovePin(GameObject pinObject)
    {
        if (pinDataDictionary.ContainsKey(pinObject))
        {
            pinDataDictionary.Remove(pinObject);
        }
        
        if (pinObject != null)
        {
            Destroy(pinObject);
        }
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: обновление позиции маркера
    public void UpdatePinPosition(GameObject pinObject, Vector2 newMapPosition)
    {
        if (pinDataDictionary.ContainsKey(pinObject))
        {
            pinDataDictionary[pinObject].mapPosition = newMapPosition;
            Debug.Log($"Updated pin position in dictionary: {newMapPosition}");
        }
        else
        {
            Debug.LogWarning($"Pin not found in dictionary: {pinObject.name}");
        }
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: обновление данных маркера
    public void UpdatePinData(GameObject pinObject, string name, string description, Texture2D image)
    {
        if (pinDataDictionary.ContainsKey(pinObject))
        {
            PinMarker.PinData pinData = pinDataDictionary[pinObject];
            pinData.name = name;
            pinData.description = description;
            pinData.image = image;
            
            Debug.Log($"Updated pin data in dictionary: name='{name}', description='{description}'");
        }
        else
        {
            Debug.LogWarning($"Pin not found in dictionary: {pinObject.name}");
        }
    }
}