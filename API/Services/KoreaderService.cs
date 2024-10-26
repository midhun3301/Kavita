using System.Threading.Tasks;
using API.Data;
using API.DTOs.Koreader;
using API.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services;

#nullable enable

public interface IKoreaderService
{
    Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId);
    Task<KoreaderBookDto> GetProgress(string bookHash, int userId);
}

public class KoreaderService : IKoreaderService
{
    private IReaderService _readerService;
    private IUnitOfWork _unitOfWork;
    private ILogger<KoreaderService> _logger;

    public KoreaderService(IReaderService readerService, IUnitOfWork unitOfWork,
        ILogger<KoreaderService> logger)
    {
        _readerService = readerService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId)
    {
        var file = await _unitOfWork.MangaFileRepository.GetByKoreaderHash(koreaderBookDto.Document);
        var userProgressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);

        _logger.LogInformation("Saving Koreader progress to Kavita: {KoreaderProgress}", koreaderBookDto.Progress);
        KoreaderHelper.UpdateProgressDto(koreaderBookDto.Progress, userProgressDto);
        await _readerService.SaveReadingProgress(userProgressDto, userId);

        await _unitOfWork.CommitAsync();
    }

    public async Task<KoreaderBookDto> GetProgress(string bookHash, int userId)
    {
        var file = await _unitOfWork.MangaFileRepository.GetByKoreaderHash(bookHash);
        var progressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);
        _logger.LogInformation("Transmitting Kavita progress to Koreader: {KoreaderProgress}", progressDto.BookScrollId);
        var koreaderProgress = KoreaderHelper.GetKoreaderPosition(progressDto);
        var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        return new KoreaderBookDto
        {
            Document = bookHash,
            Device_id = settingsDto.InstallId,
            Device = "Kavita",
            Progress = koreaderProgress,
            Percentage = progressDto.PageNum / (float) file.Pages
        };
    }
}
