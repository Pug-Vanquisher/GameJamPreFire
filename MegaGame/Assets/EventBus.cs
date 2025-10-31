using System;
using System.Collections.Generic;
using UnityEngine;

namespace Events
{
    // Шина событий для связи между системами (без прямых зависимостей)
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        // Подписка на событие (в Start() или Awake())
        public static void Subscribe<T>(Action<T> callback)
        {
            var type = typeof(T);
            if (!_subscribers.ContainsKey(type))
                _subscribers[type] = new List<Delegate>();
            _subscribers[type].Add(callback);
        }

        // Отписка от события (в OnDestroy())
        public static void Unsubscribe<T>(Action<T> callback)
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var list))
                list.Remove(callback);
        }

        // Публикация события (уведомляет всех подписчиков)
        public static void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var list)) return;

            var copy = new List<Delegate>(list);
            var toRemove = new List<Delegate>();

            foreach (var del in copy)
            {
                try
                {
                    // Если цель делегата — UnityEngine.Object и он уже разрушен — помечаем на удаление
                    if (del.Target is UnityEngine.Object uo && uo == null)
                    {
                        toRemove.Add(del);
                        continue;
                    }
                    (del as Action<T>)?.Invoke(eventData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"ошибка в событии {type.Name}: {e.Message}");
                }
            }

            // Снимаем мёртвые подписки
            foreach (var dead in toRemove) list.Remove(dead);
        }

        // Очистка всех подписок (для тестов или перезапуска)
        public static void Clear()
        {
            _subscribers.Clear();
        }

        // Получение количества подписчиков (для отладки)
        public static int GetSubscriberCount<T>()
        {
            var type = typeof(T);
            return _subscribers.TryGetValue(type, out var list) ? list.Count : 0;
        }

        // Получение всех типов событий (для отладки)
        public static string[] GetEventTypes()
        {
            var types = new string[_subscribers.Count];
            int index = 0;
            foreach (var kvp in _subscribers)
            {
                types[index++] = $"{kvp.Key.Name}: {kvp.Value.Count} подписчиков";
            }
            return types;
        }

        // Проверка наличия подписчиков (для отладки)
        public static bool HasSubscribers<T>()
        {
            var type = typeof(T);
            return _subscribers.TryGetValue(type, out var list) && list.Count > 0;
        }

    }
}