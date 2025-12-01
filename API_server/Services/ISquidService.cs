using SquidManagerAPI.Models;

namespace SquidManagerAPI.Services
{
    public interface ISquidService
    {
        #region Статус и управление службой

        /// <summary>
        /// Получить текущий статус Squid сервиса
        /// </summary>
        /// <returns>Объект с информацией о статусе</returns>
        Task<SquidStatus> GetStatusAsync();

        /// <summary>
        /// Запустить Squid сервис
        /// </summary>
        /// <returns>True если успешно, false в случае ошибки</returns>
        Task<bool> StartSquidAsync();

        /// <summary>
        /// Остановить Squid сервис
        /// </summary>
        /// <returns>True если успешно, false в случае ошибки</returns>
        Task<bool> StopSquidAsync();

        /// <summary>
        /// Перезапустить Squid сервис
        /// </summary>
        /// <returns>True если успешно, false в случае ошибки</returns>
        Task<bool> RestartSquidAsync();

        /// <summary>
        /// Перезагрузить конфигурацию Squid без перезапуска сервиса
        /// </summary>
        /// <returns>True если успешно, false в случае ошибки</returns>
        Task<bool> ReloadConfigAsync();

        /// <summary>
        /// Проверить конфигурацию Squid на наличие ошибок
        /// </summary>
        /// <returns>"OK" если проверка пройдена, иначе текст ошибки</returns>
        Task<string> TestConfigAsync();

        #endregion

        #region Управление конфигурацией

        /// <summary>
        /// Получить текущую конфигурацию Squid
        /// </summary>
        /// <returns>Список конфигурационных директив</returns>
        Task<List<SquidConfig>> GetConfigAsync();

        /// <summary>
        /// Обновить конфигурацию Squid
        /// </summary>
        /// <param name="configs">Новые конфигурационные директивы</param>
        /// <returns>True если успешно, false в случае ошибки</returns>
        Task<bool> UpdateConfigAsync(List<SquidConfig> configs);

        #endregion

        #region Работа с логами

        /// <summary>
        /// Получить последние строки из лога доступа
        /// </summary>
        /// <param name="lines">Количество строк для получения</param>
        /// <returns>Список строк лога</returns>
        Task<List<string>> GetAccessLogsAsync(int lines);

        /// <summary>
        /// Получить последние строки из лога кэша
        /// </summary>
        /// <param name="lines">Количество строк для получения</param>
        /// <returns>Список строк лога</returns>
        Task<List<string>> GetCacheLogsAsync(int lines);

        #endregion

        #region Системная информация

        /// <summary>
        /// Получить информацию о системе и установленных компонентах
        /// </summary>
        /// <returns>Объект с системной информацией</returns>
        Task<SystemInfo> GetSystemInfoAsync();

        #endregion

        #region SquidGuard

        /// <summary>
        /// Получить список доступных черных списков SquidGuard
        /// </summary>
        /// <returns>Список категорий черных списков</returns>
        Task<List<string>> GetSquidGuardBlacklistsAsync();

        /// <summary>
        /// Обновить черные списки SquidGuard
        /// </summary>
        /// <returns>True если успешно, false в случае ошибки</returns>
        Task<bool> UpdateSquidGuardAsync();

        /// <summary>
        /// Получить статистику фильтрации SquidGuard
        /// </summary>
        /// <returns>Словарь со статистическими данными</returns>
        Task<Dictionary<string, string>> GetSquidGuardStatsAsync();

        #endregion
    }
}