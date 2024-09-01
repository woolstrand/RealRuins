using System.IO;
using System.IO.Compression;
using System.Text;

namespace RealRuins;

internal class Compressor
{
	public static void ZipFile(string filename)
	{
		byte[] buffer = File.ReadAllBytes(filename);
		File.Delete(filename);
		using MemoryStream src = new MemoryStream(buffer);
		using FileStream fileStream = File.Open(filename, FileMode.Create);
		fileStream.Seek(0L, SeekOrigin.Begin);
		using GZipStream dest = new GZipStream(fileStream, CompressionMode.Compress);
		CopyTo(src, dest);
	}

	public static string UnzipFile(string filename)
	{
		using FileStream stream = File.OpenRead(filename);
		using MemoryStream memoryStream = new MemoryStream();
		using (GZipStream src = new GZipStream(stream, CompressionMode.Decompress))
		{
			CopyTo(src, memoryStream);
		}
		return Encoding.UTF8.GetString(memoryStream.ToArray());
	}

	public static void CopyTo(Stream src, Stream dest)
	{
		byte[] array = new byte[4096];
		int count;
		while ((count = src.Read(array, 0, array.Length)) != 0)
		{
			dest.Write(array, 0, count);
		}
	}
}
