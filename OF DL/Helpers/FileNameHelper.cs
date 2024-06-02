using OF_DL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OF_DL.Helpers
{
    public class FileNameHelper : IFileNameHelper
    {
        private readonly Auth auth;

        public FileNameHelper(Auth auth)
        {
            this.auth = auth;
        }

        public async Task<Dictionary<string, string>> GetFilename(object obj1, object obj2, object obj3, List<string> selectedProperties, string username, Dictionary<string, int> users = null)
        {
            Dictionary<string, string> values = new();
            Type type1 = obj1.GetType();
            Type type2 = obj2.GetType();
            PropertyInfo[] properties1 = type1.GetProperties();
            PropertyInfo[] properties2 = type2.GetProperties();

            foreach (string propertyName in selectedProperties)
            {
                if (propertyName.Contains("media"))
                {
                    object drmProperty = null;
                    object fileProperty = GetNestedPropertyValue(obj2, "files");
                    if(fileProperty != null)
                    {
                        drmProperty = GetNestedPropertyValue(obj2, "files.drm");
                    }
                     
                    if(fileProperty != null && drmProperty != null && propertyName == "mediaCreatedAt")
                    {
                        object mpdurl = GetNestedPropertyValue(obj2, "files.drm.manifest.dash");
                        object policy = GetNestedPropertyValue(obj2, "files.drm.signature.dash.CloudFrontPolicy");
                        object signature = GetNestedPropertyValue(obj2, "files.drm.signature.dash.CloudFrontSignature");
                        object kvp = GetNestedPropertyValue(obj2, "files.drm.signature.dash.CloudFrontKeyPairId");
                        DateTime lastModified = await DownloadHelper.GetDRMVideoLastModified(string.Join(",", mpdurl, policy, signature, kvp), auth);
                        values.Add(propertyName, lastModified.ToString("yyyy-MM-dd"));
                        continue;
                    }
                    else if((fileProperty == null || drmProperty == null) && propertyName == "mediaCreatedAt")
                    {
                        object source = GetNestedPropertyValue(obj2, "source.source");
                        if(source != null)
                        {
                            DateTime lastModified = await DownloadHelper.GetMediaLastModified(source.ToString());
                            values.Add(propertyName, lastModified.ToString("yyyy-MM-dd"));
                            continue;
                        }
                        else
                        {
                            object preview = GetNestedPropertyValue(obj2, "preview");
                            if(preview != null)
                            {
                                DateTime lastModified = await DownloadHelper.GetMediaLastModified(preview.ToString());
                                values.Add(propertyName, lastModified.ToString("yyyy-MM-dd"));
                                continue;
                            }
                        }
                        
                    }
                    PropertyInfo? property = Array.Find(properties2, p => p.Name.Equals(propertyName.Replace("media", ""), StringComparison.OrdinalIgnoreCase));
                    if (property != null)
                    {
                        object? propertyValue = property.GetValue(obj2);
                        if (propertyValue != null)
                        {
                            if (propertyValue is DateTime dateTimeValue)
                            {
                                values.Add(propertyName, dateTimeValue.ToString("yyyy-MM-dd"));
                            }
                            else
                            {
                                values.Add(propertyName, propertyValue.ToString());
                            }
                        }
                    }
                }
                else if (propertyName.Contains("filename"))
                {
                    PropertyInfo property = Array.Find(properties2, p => p.Name.Equals("source", StringComparison.OrdinalIgnoreCase));
                    if (property != null)
                    {
                        object propertyValue = property.GetValue(obj2);
                        if (propertyValue != null)
                        {
                            Type sourceType = propertyValue.GetType();
                            PropertyInfo[] sourceProperties = sourceType.GetProperties();
                            PropertyInfo sourceProperty = Array.Find(sourceProperties, p => p.Name.Equals("source", StringComparison.OrdinalIgnoreCase));
                            if(sourceProperty != null)
                            {
                                object sourcePropertyValue = sourceProperty.GetValue(propertyValue);
                                if(sourcePropertyValue != null)
                                {
                                    Uri uri = new(sourcePropertyValue.ToString());
                                    string filename = System.IO.Path.GetFileName(uri.LocalPath);
                                    values.Add(propertyName, filename.Split(".")[0]);
                                }
                                else
                                {
                                    string propertyPath = "files.drm.manifest.dash";
                                    object nestedPropertyValue = GetNestedPropertyValue(obj2, propertyPath);
                                    if (nestedPropertyValue != null)
                                    {
                                        Uri uri = new(nestedPropertyValue.ToString());
                                        string filename = System.IO.Path.GetFileName(uri.LocalPath);
                                        values.Add(propertyName, filename.Split(".")[0] + "_source");
                                    }
                                }
                            }
                        }
                    }
                }
                else if (propertyName.Contains("username"))
                {
                    if(!string.IsNullOrEmpty(username))
                    {
                        values.Add(propertyName, username);
                    }
                    else
                    {
                        string propertyPath = "id";
                        object nestedPropertyValue = GetNestedPropertyValue(obj3, propertyPath);
                        if (nestedPropertyValue != null)
                        {
                            values.Add(propertyName, users.FirstOrDefault(u => u.Value == Convert.ToInt32(nestedPropertyValue.ToString())).Key);
                        }
                    }
                }
                else if (propertyName.Contains("text", StringComparison.OrdinalIgnoreCase))
                {
                    PropertyInfo property = Array.Find(properties1, p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
                    if (property != null)
                    {
                        object propertyValue = property.GetValue(obj1);
                        if (propertyValue != null)
                        {
                            var str = propertyValue.ToString();
                            if (str.Length > 100) // todo: add length limit to config
                                str = str.Substring(0, 100);
                            values.Add(propertyName, str);
                        }
                    }
                }
                else
                {
                    PropertyInfo property = Array.Find(properties1, p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
                    if (property != null)
                    {
                        object propertyValue = property.GetValue(obj1);
                        if (propertyValue != null)
                        {
                            if (propertyValue is DateTime dateTimeValue)
                            {
                                values.Add(propertyName, dateTimeValue.ToString("yyyy-MM-dd"));
                            }
                            else
                            {
                                values.Add(propertyName, propertyValue.ToString());
                            }
                        }
                    }
                }
            }
            return values;
        }

        static object GetNestedPropertyValue(object source, string propertyPath)
        {
            object value = source;
            foreach (var propertyName in propertyPath.Split('.'))
            {
                PropertyInfo property = value.GetType().GetProperty(propertyName) ?? throw new ArgumentException($"Property '{propertyName}' not found.");
                value = property.GetValue(value);
            }
            return value;
        }

        public async Task<string> BuildFilename(string fileFormat, Dictionary<string, string> values)
        {
            foreach (var kvp in values)
            {
                string placeholder = "{" + kvp.Key + "}";
                fileFormat = fileFormat.Replace(placeholder, kvp.Value);
            }

            return WidevineClient.Utils.RemoveInvalidFileNameChars($"{fileFormat}");
        }
    }
}
