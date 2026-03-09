using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    public class ClearIndexCache : Popup
    {
        public ClearIndexCache(Repository repo)
        {
            _repo = repo;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Clear index cache ...";

            var log = _repo.CreateLog("Clear index cache");
            Use(log);

            await new Commands.Command
            {
                WorkingDirectory = _repo.FullPath,
                Context = _repo.FullPath,
                Args = "rm -r --cached .",
            }.Use(log).ExecAsync();

            await new Commands.Command
            {
                WorkingDirectory = _repo.FullPath,
                Context = _repo.FullPath,
                Args = "add .",
            }.Use(log).ExecAsync();

            log.Complete();
            return true;
        }

        private readonly Repository _repo = null;
    }
}
