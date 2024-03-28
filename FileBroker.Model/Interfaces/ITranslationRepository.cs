using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface ITranslationRepository
{
    Task<List<TranslationData>> GetTranslations();
}
