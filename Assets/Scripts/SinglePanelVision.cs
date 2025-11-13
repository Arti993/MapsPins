using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SinglePanelVision : MonoBehaviour
{
    // Список всех открытых панелей
    private static readonly List<PinMarker> _openPanels = new List<PinMarker>();
    
    /// <summary>
    /// Закрывает все открытые панели
    /// </summary>
    public static void CloseAllOpenPanels()
    {
        foreach (PinMarker pinMarker in _openPanels.ToArray())
        {
            if (pinMarker != null)
            {
                pinMarker.ClosePinPanel();
            }
        }
        _openPanels.Clear();
    }
    
    /// <summary>
    /// Регистрирует панель как открытую
    /// </summary>
    /// <param name="pinMarker">Ссылка на маркер с открытой панелью</param>
    public static void RegisterOpenPanel(PinMarker pinMarker)
    {
        if (pinMarker != null && !_openPanels.Contains(pinMarker))
        {
            _openPanels.Add(pinMarker);
        }
    }
    
    /// <summary>
    /// Разрегистрирует панель как закрытую
    /// </summary>
    /// <param name="pinMarker">Ссылка на маркер с закрытой панелью</param>
    public static void UnregisterOpenPanel(PinMarker pinMarker)
    {
        if (_openPanels.Contains(pinMarker))
        {
            _openPanels.Remove(pinMarker);
        }
    }
    
    /// <summary>
    /// Проверяет, открыта ли панель для данного маркера
    /// </summary>
    /// <param name="pinMarker">Маркер для проверки</param>
    /// <returns>true если панель открыта</returns>
    public static bool IsPanelOpen(PinMarker pinMarker)
    {
        return _openPanels.Contains(pinMarker);
    }
    
    /// <summary>
    /// Закрывает панель конкретного маркера
    /// </summary>
    /// <param name="pinMarker">Маркер для закрытия панели</param>
    public static void ClosePanel(PinMarker pinMarker)
    {
        if (pinMarker != null && _openPanels.Contains(pinMarker))
        {
            pinMarker.ClosePinPanel();
            UnregisterOpenPanel(pinMarker);
        }
    }
    
    /// <summary>
    /// Получает количество открытых панелей
    /// </summary>
    /// <returns>Количество открытых панелей</returns>
    public static int GetOpenPanelCount()
    {
        return _openPanels.Count;
    }
    
    /// <summary>
    /// Очищает список при завершении работы (например, при перезагрузке сцены)
    /// </summary>
    public static void ClearAllPanels()
    {
        foreach (PinMarker pinMarker in _openPanels.ToArray())
        {
            if (pinMarker != null)
            {
                pinMarker.ClosePinPanel();
            }
        }
        _openPanels.Clear();
    }
    
    private void OnDestroy()
    {
        // Очистка при уничтожении
        ClearAllPanels();
    }
    
    private void OnApplicationQuit()
    {
        // Очистка при завершении приложения
        ClearAllPanels();
    }
}