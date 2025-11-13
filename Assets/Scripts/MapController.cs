using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine.Serialization;

[RequireComponent(typeof(Camera))]
public class MapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Image _mapImage;
    
    [Header("Pin Settings")]
    [SerializeField] private GameObject _pinMarkerPrefab; // Префаб маркера с PinMarker скриптом
    [SerializeField] private float _pinTolerance = 10f; // Допустимое движение мыши в пикселях
    
    [Header("Camera Settings")]
    [SerializeField] private Vector2 _cameraScale = Vector2.one;
    [SerializeField] private float[] _zoomSteps = { 1f, 1.5f, 2f, 3f };
    
    [Header("Drag Settings")]
    [SerializeField] private float _dragMultiplier = 1f;

    private PlayerInput _playerInput;
    private Vector3 _initialCameraPosition;
    private float _initialOrthographicSize;
    private Vector2 _mapSize;
    private Vector2 _cameraBounds;
    private bool _isDragging = false;
    private Vector3 _lastMousePosition;
    private int _currentZoomIndex = 0;
    
    // 🎯 НОВОЕ: храним начальный размер изображения карты
    private Vector2 _initialMapSize;
    private Vector3 _initialMapPosition;
    
    // 🎯 НОВОЕ: для UI drag
    private Vector3 _lastMapPosition;
    private Vector2 _canvasSize;
    
    // 📍 ПЕРЕМЕННЫЕ ДЛЯ СОЗДАНИЯ МАРКЕРОВ
    private Transform _pinContainer; // Контейнер для маркеров (дочерний к карте)

    // 📋 СЛОВАРЬ ДАННЫХ МАРКЕРОВ
    private Dictionary<GameObject, PinData> _pinDataDictionary = new Dictionary<GameObject, PinData>();
    
    public float CurrentZoom => _zoomSteps[_currentZoomIndex];

    private void Awake()
    {
        _playerInput = new PlayerInput();
        
        if (_mainCamera == null)
            _mainCamera = GetComponent<Camera>();

        // Получаем размер карты
        if (_mapImage != null)
        {
            var rectTransform = _mapImage.rectTransform;
            _mapSize = rectTransform.rect.size;
            _initialMapSize = _mapSize;
            _initialMapPosition = rectTransform.localPosition;
        }
        
        // 🎯 НОВОЕ: получаем размер Canvas
        Canvas canvas = GetCanvas();
        if (canvas != null)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                _canvasSize = canvasRect.rect.size;
            }
        }
        
        // 📍 Создаём контейнер для маркеров (как дочерний к карте)
        CreatePinContainer();
    }
    
    private void Start()
    {
        // Сохраняем начальную позицию камеры и размер
        _initialCameraPosition = transform.position;
        _initialOrthographicSize = _mainCamera.orthographicSize;
        
        // Устанавливаем камеру на максимальное отдаление (масштаб 1)
        SetCameraScale(_zoomSteps[_currentZoomIndex]);
    }
    
    private void OnEnable()
    {
        if (_playerInput != null)
        {
            _playerInput.Enable();
            
            // ⚡ ИСПРАВЛЕНО: используем только performed для прокрутки
            _playerInput.UI.Zoom.performed += OnZoomPerformed;
            _playerInput.UI.Drag.started += OnDragStarted;
            _playerInput.UI.Drag.performed += OnDragPerformed;
            _playerInput.UI.Drag.canceled += OnDragCanceled;
            
            // 📍 НОВОЕ: подписываемся на события создания маркеров
            _playerInput.UI.MakePin.started += OnMakePinStarted;
            _playerInput.UI.MakePin.performed += OnMakePinPerformed;
            _playerInput.UI.MakePin.canceled += OnMakePinCanceled;
        }
    }

    private void OnDisable()
    {
        if (_playerInput != null)
        {
            _playerInput.UI.Zoom.performed -= OnZoomPerformed;
            _playerInput.UI.Drag.started -= OnDragStarted;
            _playerInput.UI.Drag.performed -= OnDragPerformed;
            _playerInput.UI.Drag.canceled -= OnDragCanceled;
            
            // 📍 НОВОЕ: отписываемся от событий создания маркеров
            _playerInput.UI.MakePin.started -= OnMakePinStarted;
            _playerInput.UI.MakePin.performed -= OnMakePinPerformed;
            _playerInput.UI.MakePin.canceled -= OnMakePinCanceled;
            
            _playerInput.Disable();
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
        _isDragging = true;
        Vector2 mousePos = Mouse.current.position.ReadValue();
        _lastMousePosition = new Vector3(mousePos.x, mousePos.y, 0);
        
        // 🎯 НОВОЕ: сохраняем начальную позицию карты
        if (_mapImage != null)
        {
            _lastMapPosition = _mapImage.rectTransform.localPosition;
        }
        
        Debug.Log("Drag started");
    }

    private void OnDragPerformed(InputAction.CallbackContext context)
    {
        if (!_isDragging) return;
        
        // 🎯 ИСПРАВЛЕНО: drag работает при любом масштабе > 1
        if (_cameraScale.x <= 1f) 
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
            _lastMousePosition = currentMousePosition;
            return;
        }
        
        Vector3 mouseDelta = currentMousePosition - _lastMousePosition;
        
        // 🎯 ИСПРАВЛЕНО: работаем с UI координатами, а не мировыми
        ApplyUIDrag(mouseDelta);
        
        _lastMousePosition = currentMousePosition;
    }

    private void OnDragCanceled(InputAction.CallbackContext context)
    {
        _isDragging = false;
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
        if (_mapImage == null)
        {
            Debug.LogError("Map image not found!");
            return;
        }
        
        // Проверяем наличие префаба маркера
        if (_pinMarkerPrefab == null)
        {
            Debug.LogError("Pin marker prefab not assigned!");
            return;
        }
        
        // Убеждаемся, что контейнер существует и правильно настроен
        if (_pinContainer == null)
        {
            CreatePinContainer();
        }
        
        if (_pinContainer == null)
        {
            Debug.LogError("PinContainer not created!");
            return;
        }
        
        Debug.Log($"Creating pin with parent: {_pinContainer.name}, map: {_mapImage.name}");
        
        // 🗺️ КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: создаём маркер из префаба
        GameObject pinObject = Instantiate(_pinMarkerPrefab, _pinContainer);
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
            PinData newPinData = new PinData("", "", null, mapLocalPosition);
            _pinDataDictionary[pinObject] = newPinData;
            
            // Подписываемся на событие сохранения данных маркера
            pinMarker.OnPinDataSaved += (name, description) => {
                // Обновляем данные в словаре при сохранении
                if (_pinDataDictionary.ContainsKey(pinObject))
                {
                    _pinDataDictionary[pinObject].name = name;
                    _pinDataDictionary[pinObject].description = description;
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
        if (_mapImage == null)
        {
            Debug.LogError("Map image not found for PinContainer!");
            return;
        }
        
        Debug.Log($"Creating PinContainer for map: {_mapImage.name}");
        
        // Ищем существующий контейнер
        Transform existingContainer = _mapImage.transform.Find("PinContainer");
        
        if (existingContainer != null)
        {
            _pinContainer = existingContainer;
            Debug.Log("Using existing PinContainer");
        }
        else
        {
            // Создаём новый контейнер как дочерний к карте
            GameObject containerObject = new GameObject("PinContainer");
            _pinContainer = containerObject.transform;
            _pinContainer.SetParent(_mapImage.transform, false);
            
            Debug.Log($"New PinContainer created as child of: {_mapImage.name}");
            
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
        if (_mapImage == null)
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
        if (_mapImage == null)
        {
            Debug.LogError("Map image not found for canvas to map conversion!");
            return canvasLocalPosition;
        }
        
        RectTransform mapRect = _mapImage.rectTransform;
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
        if (_mapImage == null) return;
        
        var rectTransform = _mapImage.rectTransform;
        
        // Вычисляем движение с учетом текущего масштаба карты
        // Чем больше масштаб, тем меньше нужно двигать для компенсации
        float scaleFactor = 1f / _cameraScale.x;
        
        Vector3 newPosition = _lastMapPosition + mouseDelta * _dragMultiplier * scaleFactor;
        
        // Применяем новую позицию
        rectTransform.localPosition = newPosition;
        
        // Обновляем запомненную позицию
        _lastMapPosition = newPosition;
        
        // Ограничиваем движение в пределах экрана
        ClampMapPosition();
        
        Debug.Log($"Map moved to: {rectTransform.localPosition}");
    }
    
    // 🎯 НОВЫЙ МЕТОД: ограничение позиции карты
    private void ClampMapPosition()
    {
        if (_mapImage == null) return;
        
        var rectTransform = _mapImage.rectTransform;
        
        // Получаем текущие размеры карты с учетом масштаба
        float scaledMapWidth = _initialMapSize.x * _cameraScale.x;
        float scaledMapHeight = _initialMapSize.y * _cameraScale.y;
        
        // Вычисляем границы, чтобы карта не выходила за края экрана
        float halfScaledMapWidth = scaledMapWidth * 0.5f;
        float halfScaledMapHeight = scaledMapHeight * 0.5f;
        
        // Границы экрана
        float screenHalfWidth = _canvasSize.x * 0.5f;
        float screenHalfHeight = _canvasSize.y * 0.5f;
        
        // Если карта меньше экрана - центрируем её
        if (scaledMapWidth <= _canvasSize.x && scaledMapHeight <= _canvasSize.y)
        {
            rectTransform.localPosition = Vector3.zero;
            return;
        }
        
        // Ограничиваем позицию карты
        Vector3 currentPos = rectTransform.localPosition;
        currentPos.x = Mathf.Clamp(currentPos.x, -halfScaledMapWidth + screenHalfWidth, halfScaledMapWidth - screenHalfWidth);
        currentPos.y = Mathf.Clamp(currentPos.y, -halfScaledMapHeight + screenHalfHeight, halfScaledMapHeight - screenHalfHeight);
        currentPos.z = _initialMapPosition.z; // Сохраняем Z координату
        
        rectTransform.localPosition = currentPos;
    }

    private void HandleZoom(float scrollValue)
    {
        Debug.Log($"HandleZoom called with: {scrollValue}");
        
        int newZoomIndex = _currentZoomIndex;
        
        if (scrollValue > 0)
        {
            // Прокрутка вверх - приближение
            newZoomIndex = Mathf.Clamp(_currentZoomIndex + 1, 0, _zoomSteps.Length - 1);
            Debug.Log("Zooming in");
        }
        else if (scrollValue < 0)
        {
            // Прокрутка вниз - отдаление
            newZoomIndex = Mathf.Clamp(_currentZoomIndex - 1, 0, _zoomSteps.Length - 1);
            Debug.Log("Zooming out");
        }

        if (newZoomIndex != _currentZoomIndex)
        {
            _currentZoomIndex = newZoomIndex;
            Debug.Log($"Setting camera scale to: {_zoomSteps[_currentZoomIndex]}");
            SetCameraScale(_zoomSteps[_currentZoomIndex]);
        }
        else
        {
            Debug.Log("Zoom index unchanged");
        }
    }

    private void SetCameraScale(float scale)
    {
        _cameraScale = Vector2.one * scale;
        
        // 🎯 ПРИМЕНЯЕМ МАСШТАБ К UI КАРТЕ
        ApplyMapScale();
        
        // Применяем масштаб к камере
        UpdateCameraProjection();
        
        // Получаем размер видимой области камеры в мировых координатах
        float worldHeight = _mainCamera.orthographicSize * 2f;
        float worldWidth = worldHeight * _mainCamera.aspect;
        
        _cameraBounds = new Vector2(worldWidth, worldHeight);
        
        Debug.Log($"Camera orthographic size: {_mainCamera.orthographicSize}");
        Debug.Log($"Map scale: {_mapImage.rectTransform.localScale}");
        
        // Проверяем, что камера может показать всю карту при данном масштабе
        if (_cameraScale.x <= 1f)
        {
            // При максимальном отдалении (масштаб 1) центрируем камеру и карту
            transform.position = new Vector3(_initialCameraPosition.x, _initialCameraPosition.y, _initialCameraPosition.z);
            if (_mapImage != null)
            {
                _mapImage.rectTransform.localPosition = _initialMapPosition;
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
        if (_mapImage == null) return;
        
        var rectTransform = _mapImage.rectTransform;
        
        // Применяем масштаб к изображению карты
        rectTransform.localScale = new Vector3(_cameraScale.x, _cameraScale.y, 1f);
    }

    private void UpdateCameraProjection()
    {
        // Применяем масштаб к камере (исправленная формула)
        _mainCamera.orthographicSize = _initialOrthographicSize / _cameraScale.y;
    }
    
    // 🎯 ВСПОМОГАТЕЛЬНЫЙ МЕТОД: получить Canvas
    private Canvas GetCanvas()
    {
        if (_mapImage != null)
        {
            return _mapImage.GetComponentInParent<Canvas>();
        }
        return null;
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: получение всех маркеров и их данных
    public Dictionary<GameObject, PinData> GetAllPinsData()
    {
        return _pinDataDictionary;
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: удаление маркера по объекту
    public void RemovePin(GameObject pinObject)
    {
        if (_pinDataDictionary.ContainsKey(pinObject))
        {
            _pinDataDictionary.Remove(pinObject);
        }
        
        if (pinObject != null)
        {
            Destroy(pinObject);
        }
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: обновление позиции маркера
    public void UpdatePinPosition(GameObject pinObject, Vector2 newMapPosition)
    {
        if (_pinDataDictionary.ContainsKey(pinObject))
        {
            _pinDataDictionary[pinObject].mapPosition = newMapPosition;
            Debug.Log($"Updated pin position in dictionary: {newMapPosition}");
        }
        else
        {
            Debug.LogWarning($"Pin not found in dictionary: {pinObject.name}");
        }
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: получение контейнера для маркеров
    public Transform GetPinContainer()
    {
        // Убеждаемся, что контейнер существует
        if (_pinContainer == null)
        {
            CreatePinContainer();
        }
        
        return _pinContainer;
    }
    
    // 🆕 ПУБЛИЧНЫЙ МЕТОД: обновление данных маркера
    public void UpdatePinData(GameObject pinObject, string name, string description, Texture2D image)
    {
        if (_pinDataDictionary.ContainsKey(pinObject))
        {
            PinData pinData = _pinDataDictionary[pinObject];
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