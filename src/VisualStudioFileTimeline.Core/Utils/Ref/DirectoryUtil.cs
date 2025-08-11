
using System.Extensions;

namespace System.IO;

//TODO 测试

/// <summary>
/// 目录工具
/// </summary>
public static class DirectoryUtil
{
    /// <summary>
    /// 将目录 <paramref name="sourceDirectory"/> 复制到 <paramref name="destDirectory"/>
    /// <br/> - 如果 <paramref name="destDirectory"/> 不存在则会创建
    /// </summary>
    /// <param name="sourceDirectory">原目录</param>
    /// <param name="destDirectory">目标目录</param>
    /// <param name="overwrite">是否覆盖文件</param>
    /// <param name="recursive">是否递归多级目录</param>
    /// <param name="ignoreItemError">是否忽略单项错误</param>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static void Copy(string sourceDirectory,
                            string destDirectory,
                            bool overwrite = false,
                            bool recursive = false,
                            bool ignoreItemError = false)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
        }

        Ensure(destDirectory);

        //复制文件
        foreach (var sourceFilePath in Directory.EnumerateFiles(sourceDirectory))
        {
            try
            {
                var destFilePath = Path.Combine(destDirectory, Path.GetFileName(sourceFilePath));
                File.Copy(sourceFilePath, destFilePath, overwrite);
            }
            catch
            {
                if (!ignoreItemError)
                {
                    throw;
                }
            }
        }

        if (recursive)
        {
            //复制目录
            foreach (var sourceSubDirectory in Directory.EnumerateDirectories(sourceDirectory))
            {
                try
                {
                    var destSubDirectory = Path.Combine(destDirectory, Path.GetFileName(sourceSubDirectory));

                    Copy(sourceDirectory: sourceSubDirectory,
                         destDirectory: destSubDirectory,
                         overwrite: overwrite,
                         recursive: recursive,
                         ignoreItemError: ignoreItemError);
                }
                catch
                {
                    if (!ignoreItemError)
                    {
                        throw;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 确保路径<paramref name="directory"/>存在，如果不存在，则会尝试创建该路径的目录
    /// </summary>
    /// <param name="directory"></param>
    public static void Ensure(string directory)
    {
        if (!Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch
            {
                //已创建情况不抛出异常
                if (!Directory.Exists(directory))
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// 获取一个在<see cref="Path.GetTempPath"/>路径下创建的Guid目录
    /// </summary>
    public static string GetTempPath()
    {
        var tmpDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToHexString());

        Ensure(tmpDirectory);

        return tmpDirectory;
    }

    /// <summary>
    /// 静默删除文件夹(吞并所有异常)
    /// </summary>
    /// <param name="directory"></param>
    /// <returns>是否成功删除（目录已不存在）</returns>
    public static bool SilenceDelete(string directory)
    {
        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                try
                {
                    Console.Error.WriteLine($"\"{directory}\" delete fail with: {ex.Message}.");
                }
                catch { }
                return false;
            }
        }
        return true;
    }
}
