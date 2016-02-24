using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ies2csv
{
	class Program
	{
		static string GetString(BinaryReader br, int len)
		{
			var c = br.ReadBytes(len);
			for(var i = 0; i < c.Length; i++)
				if (c[i] != 0)
					c[i] = (byte)(c[i] ^ 0x1);

			var str = Encoding.UTF8.GetString(c);
			return str.TrimEnd('\0');
		}

		static void Convert(string inputpath, string outputpath)
		{
			var fin = File.ReadAllBytes(inputpath);
			var ms = new MemoryStream(fin);
			var br = new BinaryReader(ms);

			var tablename = br.ReadChars(128).ToString().TrimEnd('\0');
			var val1 = br.ReadInt32();
			var offset1 = br.ReadInt32();
			var offset2 = br.ReadInt32();
			var filesize = br.ReadInt32();

			Debug.Assert(fin.Length == filesize, "IES file has invalid length specified: " + inputpath);
			Debug.Assert(val1 == 1, "IES file has incorrect value " + inputpath);

			var short1 = br.ReadInt16();
			var rows = br.ReadInt16();
			var cols = br.ReadInt16();
			var colint = br.ReadInt16();
			var colstr = br.ReadInt16();

			Debug.Assert(colint + colstr == cols);

			var intpos = new List<int>();
			var strpos = new List<int>();

			ms.Seek(filesize - (offset1 + offset2), SeekOrigin.Begin);

			var colnames = new string[cols];
			
			for (var i = 0; i < cols; i++)
			{
				var n1 = GetString(br, 64); //.ToString().TrimEnd('\0');
				var n2 = GetString(br, 64);
				var type = br.ReadInt16();
				var dummy = br.ReadInt32();
				var pos = br.ReadInt16();
				
				if (type == 0)
				{
					Debug.Assert(colnames[pos] == null);
					intpos.Add(i);
					colnames[pos] = n1;
					
				}
				else
				{
					Debug.Assert(colnames[pos + colint] == null);
					strpos.Add(i);
					colnames[pos + colint] = n1;
				}
			}

			var txt = File.CreateText(outputpath);
			var csv = new CsvHelper.CsvWriter(txt);

			for (var i = 0; i < cols; i++)
			{
				Debug.Assert(colnames[i] != null);
				csv.WriteField(colnames[i]);
			}


			csv.NextRecord();
			
			ms.Seek(filesize - offset2, SeekOrigin.Begin);
			
			for (var i = 0; i < rows; i++)
			{
				var rowid = br.ReadInt32();
				var l = br.ReadInt16();
				var lookupkey = GetString(br, l);

				var objs = new Object[cols];

				var pos = 0;

				for (var j = 0; j < colint; j++)
				{
					var i1 = br.ReadSingle();
					objs[j] = i1;
				}

				for (var j = 0; j < colstr; j++)
				{
					//str
					var len = br.ReadUInt16();
					//Debug.Assert(len >= 0);
					var str = GetString(br, len);
					objs[j + colint] = str;
				}

				foreach (var o in objs)
				{
					Debug.Assert(o != null);
					if(o is float)
						csv.WriteField((float)o);
					else
						csv.WriteField((string)o);
				}
				
				ms.Seek(colstr, SeekOrigin.Current);
				csv.NextRecord();
			}

			txt.Close();
		}

		static void Main(string[] args)
		{

			foreach (var f in Directory.GetFiles(@".", "*.ies", SearchOption.AllDirectories))
			{
				Console.WriteLine("Converting " + f);
				Convert(f, f + ".csv");
			}
			//Convert(@"D:\Dropbox\treeofsavior\ies.ipf\item.ies", @"D:\Dropbox\treeofsavior\ies.ipf\item.ies.csv");
		}
	}
}
