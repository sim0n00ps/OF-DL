using OF_DL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Helpers
{
    internal interface IDownloadContext
    {
        public IDownloadConfig DownloadConfig { get; }
        public IFileNameFormatConfig FileNameFormatConfig { get; }
        public APIHelper ApiHelper { get; }
        public DBHelper DBHelper { get; }
        public DownloadHelper DownloadHelper { get; }
    }

    internal class DownloadContext : IDownloadContext
    {
        public APIHelper ApiHelper { get; }
        public DBHelper DBHelper { get; }
        public DownloadHelper DownloadHelper { get; }
        public IDownloadConfig DownloadConfig { get; }
        public IFileNameFormatConfig FileNameFormatConfig { get; }

        public DownloadContext(Auth auth, IDownloadConfig downloadConfig, IFileNameFormatConfig fileNameFormatConfig, APIHelper apiHelper, DBHelper dBHelper)
        {
            ApiHelper = apiHelper;
            DBHelper = dBHelper;
            DownloadConfig = downloadConfig;
            FileNameFormatConfig = fileNameFormatConfig;
            DownloadHelper = new DownloadHelper(auth, downloadConfig, fileNameFormatConfig);
        }
    }
}
