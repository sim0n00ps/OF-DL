using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OF_DL.Enumurations;
using System.IO;
using Microsoft.Data.Sqlite;
using Serilog;

namespace OF_DL.Helpers
{
    public class DBHelper : IDBHelper
    {
        public async Task CreateDB(string folder)
        {
            try
            {
                if (!Directory.Exists(folder + "/Metadata"))
                {
                    Directory.CreateDirectory(folder + "/Metadata");
                }

                string dbFilePath = $"{folder}/Metadata/user_data.db";

                // connect to the new database file
                using SqliteConnection connection = new($"Data Source={dbFilePath}");
                // open the connection
                connection.Open();

                // create the 'medias' table
                using (SqliteCommand cmd = new("CREATE TABLE IF NOT EXISTS medias (id INTEGER NOT NULL, media_id INTEGER, post_id INTEGER NOT NULL, link VARCHAR, directory VARCHAR, filename VARCHAR, size INTEGER, api_type VARCHAR, media_type VARCHAR, preview INTEGER, linked VARCHAR, downloaded INTEGER, created_at TIMESTAMP, PRIMARY KEY(id), UNIQUE(media_id));", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // create the 'messages' table
                using (SqliteCommand cmd = new("CREATE TABLE IF NOT EXISTS messages (id INTEGER NOT NULL, post_id INTEGER NOT NULL, text VARCHAR, price INTEGER, paid INTEGER, archived BOOLEAN, created_at TIMESTAMP, user_id INTEGER, PRIMARY KEY(id), UNIQUE(post_id));", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // create the 'posts' table
                using (SqliteCommand cmd = new("CREATE TABLE IF NOT EXISTS posts (id INTEGER NOT NULL, post_id INTEGER NOT NULL, text VARCHAR, price INTEGER, paid INTEGER, archived BOOLEAN, created_at TIMESTAMP, PRIMARY KEY(id), UNIQUE(post_id));", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // create the 'stories' table
                using (SqliteCommand cmd = new("CREATE TABLE IF NOT EXISTS stories (id INTEGER NOT NULL, post_id INTEGER NOT NULL, text VARCHAR, price INTEGER, paid INTEGER, archived BOOLEAN, created_at TIMESTAMP, PRIMARY KEY(id), UNIQUE(post_id));", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // create the 'others' table
                using (SqliteCommand cmd = new("CREATE TABLE IF NOT EXISTS others (id INTEGER NOT NULL, post_id INTEGER NOT NULL, text VARCHAR, price INTEGER, paid INTEGER, archived BOOLEAN, created_at TIMESTAMP, PRIMARY KEY(id), UNIQUE(post_id));", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // create the 'products' table
                using (SqliteCommand cmd = new("CREATE TABLE IF NOT EXISTS products (id INTEGER NOT NULL, post_id INTEGER NOT NULL, text VARCHAR, price INTEGER, paid INTEGER, archived BOOLEAN, created_at TIMESTAMP, title VARCHAR, PRIMARY KEY(id), UNIQUE(post_id));", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // create the 'profiles' table
                using (SqliteCommand cmd = new("CREATE TABLE IF NOT EXISTS profiles (id INTEGER NOT NULL, user_id INTEGER NOT NULL, username VARCHAR NOT NULL, PRIMARY KEY(id), UNIQUE(username));", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                    Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
        }


        public async Task AddMessage(string folder, long post_id, string message_text, string price, bool is_paid, bool is_archived, DateTime created_at, int user_id)
        {
            try
            {
                using SqliteConnection connection = new($"Data Source={folder}/Metadata/user_data.db");
                connection.Open();
                using SqliteCommand cmd = new($"SELECT COUNT(*) FROM messages WHERE post_id=@post_id", connection);
                cmd.Parameters.AddWithValue("@post_id", post_id);
                int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count == 0)
                {
                    // If the record doesn't exist, insert a new one
                    using SqliteCommand insertCmd = new("INSERT INTO messages(post_id, text, price, paid, archived, created_at, user_id) VALUES(@post_id, @message_text, @price, @is_paid, @is_archived, @created_at, @user_id)", connection);
                    insertCmd.Parameters.AddWithValue("@post_id", post_id);
                    insertCmd.Parameters.AddWithValue("@message_text", message_text ?? (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@price", price ?? (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@is_paid", is_paid);
                    insertCmd.Parameters.AddWithValue("@is_archived", is_archived);
                    insertCmd.Parameters.AddWithValue("@created_at", created_at);
                    insertCmd.Parameters.AddWithValue("@user_id", user_id);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                    Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
        }


        public async Task AddPost(string folder, long post_id, string message_text, string price, bool is_paid, bool is_archived, DateTime created_at)
        {
            try
            {
                using SqliteConnection connection = new($"Data Source={folder}/Metadata/user_data.db");
                connection.Open();
                using SqliteCommand cmd = new($"SELECT COUNT(*) FROM posts WHERE post_id=@post_id", connection);
                cmd.Parameters.AddWithValue("@post_id", post_id);
                int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count == 0)
                {
                    // If the record doesn't exist, insert a new one
                    using SqliteCommand insertCmd = new("INSERT INTO posts(post_id, text, price, paid, archived, created_at) VALUES(@post_id, @message_text, @price, @is_paid, @is_archived, @created_at)", connection);
                    insertCmd.Parameters.AddWithValue("@post_id", post_id);
                    insertCmd.Parameters.AddWithValue("@message_text", message_text ?? (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@price", price ?? (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@is_paid", is_paid);
                    insertCmd.Parameters.AddWithValue("@is_archived", is_archived);
                    insertCmd.Parameters.AddWithValue("@created_at", created_at);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                    Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
        }


        public async Task AddStory(string folder, long post_id, string message_text, string price, bool is_paid, bool is_archived, DateTime created_at)
        {
            try
            {
                using SqliteConnection connection = new($"Data Source={folder}/Metadata/user_data.db");
                connection.Open();
                using SqliteCommand cmd = new($"SELECT COUNT(*) FROM stories WHERE post_id=@post_id", connection);
                cmd.Parameters.AddWithValue("@post_id", post_id);
                int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count == 0)
                {
                    // If the record doesn't exist, insert a new one
                    using SqliteCommand insertCmd = new("INSERT INTO stories(post_id, text, price, paid, archived, created_at) VALUES(@post_id, @message_text, @price, @is_paid, @is_archived, @created_at)", connection);
                    insertCmd.Parameters.AddWithValue("@post_id", post_id);
                    insertCmd.Parameters.AddWithValue("@message_text", message_text ?? (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@price", price ?? (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@is_paid", is_paid);
                    insertCmd.Parameters.AddWithValue("@is_archived", is_archived);
                    insertCmd.Parameters.AddWithValue("@created_at", created_at);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                    Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
        }


        public async Task AddMedia(string folder, long media_id, long post_id, string link, string? directory, string? filename, long? size, string api_type, string media_type, bool preview, bool downloaded, DateTime? created_at)
        {
            try
            {
                using SqliteConnection connection = new($"Data Source={folder}/Metadata/user_data.db");
                connection.Open();
                using SqliteCommand cmd = new($"SELECT COUNT(*) FROM medias WHERE media_id={media_id}", connection);
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                if (count == 0)
                {
                    // If the record doesn't exist, insert a new one
                    using SqliteCommand insertCmd = new($"INSERT INTO medias(media_id, post_id, link, directory, filename, size, api_type, media_type, preview, downloaded, created_at) VALUES({media_id}, {post_id}, '{link}', '{directory?.ToString() ?? "NULL"}', '{filename?.ToString() ?? "NULL"}', {size?.ToString() ?? "NULL"}, '{api_type}', '{media_type}', {Convert.ToInt32(preview)}, {Convert.ToInt32(downloaded)}, '{created_at?.ToString("yyyy-MM-dd HH:mm:ss")}')", connection);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                    Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
        }


        public async Task<bool> CheckDownloaded(string folder, long media_id)
        {
            try
            {
                bool downloaded = false;
                using (SqliteConnection connection = new($"Data Source={folder}/Metadata/user_data.db"))
                {
                    connection.Open();
                    using SqliteCommand cmd = new($"SELECT downloaded FROM medias WHERE media_id={media_id}", connection);
                    downloaded = Convert.ToBoolean(await cmd.ExecuteScalarAsync());
                }
                return downloaded;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                Log.Error("Exception caught: {0}\n\nStackTrace: {1}", ex.Message, ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("\nInner Exception:");
                    Console.WriteLine("Exception caught: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                    Log.Error("Inner Exception: {0}\n\nStackTrace: {1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                }
            }
            return false;
        }


        public async Task UpdateMedia(string folder, long media_id, string directory, string filename, long size, bool downloaded, DateTime created_at)
        {
            using SqliteConnection connection = new($"Data Source={folder}/Metadata/user_data.db");
            connection.Open();

            // Construct the update command
            string commandText = "UPDATE medias SET directory=@directory, filename=@filename, size=@size, downloaded=@downloaded, created_at=@created_at WHERE media_id=@media_id";

            // Create a new command object
            using SqliteCommand command = new(commandText, connection);
            // Add parameters to the command object
            command.Parameters.AddWithValue("@directory", directory);
            command.Parameters.AddWithValue("@filename", filename);
            command.Parameters.AddWithValue("@size", size);
            command.Parameters.AddWithValue("@downloaded", downloaded ? 1 : 0);
            command.Parameters.AddWithValue("@created_at", created_at);
            command.Parameters.AddWithValue("@media_id", media_id);

            // Execute the command
            await command.ExecuteNonQueryAsync();
        }


        public async Task<long> GetStoredFileSize(string folder, long media_id)
        {
            long size;
            using (SqliteConnection connection = new($"Data Source={folder}/Metadata/user_data.db"))
            {
                connection.Open();
                using SqliteCommand cmd = new($"SELECT size FROM medias WHERE media_id={media_id}", connection);
                size = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }
            return size;
        }
    }
}
