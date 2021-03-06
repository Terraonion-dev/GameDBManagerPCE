﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace GameDbManager
{
	public partial class GameDBMgr : Form
	{
		public class GenreTable
		{
			public int ID;
			public String Name;
		}

		public List<GenreTable> genreTable = new List<GenreTable>();

		public void AddGenre(int id, string name)
		{
			GameDB.GenreRow genre = gameDB.Genre.NewGenreRow();
			genre.Name = name;
			gameDB.Genre.AddGenreRow(genre);
		}

		public GameDBMgr()
		{
			InitializeComponent();

			if (File.Exists("db.xml"))
				gameDB.ReadXml("db.xml");

			Crc32.gen_crc_table();

			//Don't change the ID or order of this table, as it must match the one in the firmware!!
			if (gameDB.Genre.Count == 0)
			{
				AddGenre(1, "Shooter");
				AddGenre(2, "Action");
				AddGenre(3, "Sports");
				AddGenre(4, "Misc");
				AddGenre(5, "Casino");
				AddGenre(6, "Driving");
				AddGenre(7, "Platform");
				AddGenre(8, "Puzzle");
				AddGenre(9, "Boxing");
				AddGenre(10, "Wrestling");
				AddGenre(11, "Strategy");
				AddGenre(12, "Soccer");
				AddGenre(13, "Golf");
				AddGenre(14, "Beat'em-Up");
				AddGenre(15, "Baseball");
				AddGenre(16, "Mahjong");
				AddGenre(17, "Board");
				AddGenre(18, "Tennis");
				AddGenre(19, "Fighter");
				AddGenre(20, "Horse Racing");
				AddGenre(21, "Other");
			}

			genreBindingSource.DataSource = genreTable;
		}

		private void bindingSource1_CurrentChanged(object sender, EventArgs e)
		{

		}

		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
			gameDB.WriteXml("db.xml");
		}

		private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.ColumnIndex == 3)  //screenshot
			{
				OpenFileDialog ofd = new OpenFileDialog();
				ofd.Filter = "Images (*.png)|*.png";
				ofd.ValidateNames = true;
				ofd.CheckFileExists = true;
				DialogResult dr = ofd.ShowDialog();
				if (dr == DialogResult.OK)
				{
					string cwd = Directory.GetCurrentDirectory();
					string fname = ofd.FileName;

					if (fname.StartsWith(cwd))
						fname = fname.Substring(cwd.Length + 1);

					dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = fname;
				}
			}
			else if (e.ColumnIndex == 4)	//hash
			{
				Hashes hh = new GameDbManager.Hashes();

				GameDB.GameRow gr = (GameDB.GameRow)((DataRowView)dataGridView1.Rows[e.RowIndex].DataBoundItem).Row;

				hh.gdb = gameDB;
				hh.gameID = gr.ID;

				hh.ShowDialog();
			}
		}


		private void button1_Click(object sender, EventArgs e)
		{
			if (!Directory.Exists("TileCache"))
				Directory.CreateDirectory("TileCache");



			foreach (var game in gameDB.Game)
			{
				if (!game.IsScreenshotNull())
				{
					if (!String.IsNullOrEmpty(game.Screenshot))
					{
						//String dst = "TileCache/" + game.Screenshot.Substring(game.Screenshot.LastIndexOfAny(new char[] { '\\', '/' }) + 1);
						string dst = "TileCache/" + game.Screenshot.Replace("\\", "_").Replace("/", "_").Replace(":", "_");

						dst = dst.Replace(".png", ".tile");

						if (!File.Exists(dst) || File.GetLastWriteTime(game.Screenshot) > File.GetLastWriteTime(dst))
						{
							if (!Quantizer.Program.Quantize(game.Screenshot, dst))
							{
							}
						}
					}
				}
			}
		}

		class Game
		{
			public uint checksum;
			public ushort remap;
			public int GameID;
		};

		static byte ProcessChar(byte b)
		{
			if (b < 'A' || b > 'z')
				return b;
			return (byte)Char.ToUpperInvariant((Char)b);
		}


		static void ScanDir(String dir, GameDB db, GameDBMgr form)
		{
			Game[] games;
			byte[] scshots;
			ushort sccnt;
			ushort gamecnt;

			gamecnt = 0;
			sccnt = 0;
			games = new Game[1024];
			scshots = new Byte[1024 * 3072];

			if (form != null)
			{
				form.Invoke(new Action(() =>
				{
					form.label1.Text = Path.GetFileName(dir);
				}));
			}
			else
			{
				Console.WriteLine("Processing directory " + Path.GetFileName(dir));
			}


			foreach (var f in Directory.GetFiles(dir, "*.pce"))
			{
				var data = File.ReadAllBytes(f);

				var crc = Crc32.Compute(data);

				var ck = db.GameCk.FirstOrDefault(w => w.Checksum == crc.ToString("X8"));

				if (ck == null && (data.Length & 0xFFF) != 0)
				{
					uint l = (uint)data.Length & 0xFFFFF000;

					var dat = new byte[l];
					Array.Copy(data, data.Length & 0xFFF, dat, 0, l);

					data = dat;

					crc = Crc32.Compute(data);

					ck = db.GameCk.FirstOrDefault(w => w.Checksum == crc.ToString("X8"));

				}

				if (ck != null)
				{
					//var namecrc = Crc32.Compute(Encoding.ASCII.GetBytes(f.Substring(f.LastIndexOf('\\') + 1)));
					var nn = f.Substring(f.LastIndexOfAny(new char[] { '\\', '/' }) + 1);
					nn = nn.Substring(0, nn.LastIndexOf('.'));

					var name = Encoding.ASCII.GetBytes(nn);
					byte[] namecnv = new byte[56];

					if (name.Length > 56)
						Array.Copy(name, namecnv, 56);
					else
						Array.Copy(name, namecnv, name.Length);

					for (int i = 0; i < namecnv.Length; ++i)
					{
						namecnv[i] = ProcessChar(namecnv[i]);
					}

					var namecrc = Crc32.update_crc(0xFFFFFFFF, namecnv, 56);

					var existing = games.Take(gamecnt).FirstOrDefault(x => x.checksum != 0 && x.GameID == ck.GameID);

					if (existing != null)
					{
						var g = new Game();
						g.checksum = namecrc;
						g.remap = existing.remap;
						g.GameID = existing.GameID;

						games[gamecnt] = g;
						++gamecnt;
					}
					else
					{
						var gg = db.Game.FirstOrDefault(x => x.ID == ck.GameID);

						if (gg != null && !gg.IsScreenshotNull())
						{
							//String dst = "TileCache/" + gg.Screenshot.Substring(gg.Screenshot.LastIndexOfAny(new char[] { '\\', '/' }) + 1);
							string dst = "TileCache/" + gg.Screenshot.Replace("\\", "_").Replace("/", "_").Replace(":", "_");

							dst = dst.Replace(".png", ".tile");

							if (File.Exists(dst))
							{
								var g = new Game();
								g.checksum = namecrc;
								g.remap = sccnt;
								g.GameID = ck.GameID;

								games[gamecnt] = g;
								++gamecnt;

								//copy screenshot to the scshot block
								byte[] scshot = File.ReadAllBytes(dst);

								byte[] scshot2 = new byte[3072];
								Array.Copy(scshot, scshot2, scshot.Length);

								//add year, genre...
								scshot2[0xA00] = 0x0;   //version
								if (!gg.IsGenreNull())
									scshot2[0xA01] = (byte)gg.Genre;

								if (!gg.IsYearNull())
								{
									scshot2[0xA02] = (byte)(gg.Year & 0xFF);
									scshot2[0xA03] = (byte)((gg.Year >> 8) & 0xFF);
								}

								Debug.Assert(scshot2.Length == 3072);
								Array.Copy(scshot2, 0, scshots, 3072 * sccnt, scshot2.Length);

								sccnt++;
							}
						}
					}
				}
				else
				{
					//Console.WriteLine("Missing crc " + f);
				}
			}

			//scan dirs for cds
			foreach (var d in Directory.GetDirectories(dir))
			{
				var cues = Directory.GetFiles(d, "*.cue");
				if (cues.Length == 1)
				{
					try
					{
						var cue = new CueSheet(cues[0]);
						var crc = ComputeCueCrc(cue);


						var ck = db.GameCk.FirstOrDefault(w => w.Checksum == crc.ToString("X8"));

						if (ck != null)
						{
							//var namecrc = Crc32.Compute(Encoding.ASCII.GetBytes(f.Substring(f.LastIndexOf('\\') + 1)));
							var nn = d.Substring(d.LastIndexOfAny(new char[] { '\\', '/' }) + 1);

							var name = Encoding.ASCII.GetBytes(nn);
							byte[] namecnv = new byte[56];

							if (name.Length > 56)
								Array.Copy(name, namecnv, 56);
							else
								Array.Copy(name, namecnv, name.Length);

							for (int i = 0; i < namecnv.Length; ++i)
							{
								namecnv[i] = ProcessChar(namecnv[i]);
							}

							var namecrc = Crc32.update_crc(0xFFFFFFFF, namecnv, 56);

							var existing = games.Take(gamecnt).FirstOrDefault(x => x.checksum != 0 && x.GameID == ck.GameID);

							if (existing != null)
							{
								var g = new Game();
								g.checksum = namecrc;
								g.remap = existing.remap;
								g.GameID = existing.GameID;

								games[gamecnt] = g;
								++gamecnt;
							}
							else
							{
								var gg = db.Game.FirstOrDefault(x => x.ID == ck.GameID);

								if (gg != null && !gg.IsScreenshotNull())
								{
									//String dst = "TileCache/" + gg.Screenshot.Substring(gg.Screenshot.LastIndexOfAny(new char[] { '\\', '/' }) + 1);
									string dst = "TileCache/" + gg.Screenshot.Replace("\\", "_").Replace("/", "_").Replace(":", "_");

									dst = dst.Replace(".png", ".tile");

									if (File.Exists(dst))
									{
										var g = new Game();
										g.checksum = namecrc;
										g.remap = sccnt;
										g.GameID = ck.GameID;

										games[gamecnt] = g;
										++gamecnt;

										//copy screenshot to the scshot block
										byte[] scshot = File.ReadAllBytes(dst);

										byte[] scshot2 = new byte[3072];
										Array.Copy(scshot, scshot2, scshot.Length);

										//add year, genre...
										scshot2[0xA00] = 0x0;   //version
										if (!gg.IsGenreNull())
											scshot2[0xA01] = (byte)gg.Genre;

										if (!gg.IsYearNull())
										{
											scshot2[0xA02] = (byte)(gg.Year & 0xFF);
											scshot2[0xA03] = (byte)((gg.Year >> 8) & 0xFF);
										}

										Debug.Assert(scshot2.Length == 3072);
										Array.Copy(scshot2, 0, scshots, 3072 * sccnt, scshot2.Length);

										sccnt++;
									}
								}
							}
						}
						else
						{
							//Console.WriteLine("Missing crc " + f);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error processing " + cues[0] +" . Skipped");
						Console.WriteLine(ex.Message);
					}

				}
			}

			if (gamecnt != 0)
			{
				games = games.Take(gamecnt).OrderBy(x => x.checksum).ToArray();

				//Console.WriteLine("Writting game.db");

				FileStream fs = new FileStream(dir + "/games.db", FileMode.Create, FileAccess.Write);
				BinaryWriter bw = new BinaryWriter(fs);

				foreach (var g in games)
				{
					bw.Write(g.checksum);
				}

				for (int i = 0; i < 1024 - games.Length; ++i)
					bw.Write(0xFFFFFFFF);

				foreach (var g in games)
				{
					bw.Write(g.remap);
				}

				for (int i = 0; i < 1024 - games.Length; ++i)
					bw.Write((ushort)0xFFFF);

				bw.Write(scshots, 0, 3072 * sccnt);

				bw.Close();
				fs.Close();
			}

			foreach (var d in Directory.GetDirectories(dir))
				ScanDir(d, db, form);
		}


		Thread thProcessing = null;
		private void button2_Click(object sender, EventArgs e)
		{
			progressBar1.Value = 0;
			//String dir = @"L:\hucard\hu2";
			//String dir = @"i:\\";
			FolderBrowserDialog fbd = new FolderBrowserDialog();
			fbd.ShowNewFolderButton = false;

			if (fbd.ShowDialog() == DialogResult.OK)
			{
				button1.Enabled = false;
				button2.Enabled = false;
				progressBar1.Style = ProgressBarStyle.Marquee;
				thProcessing = new Thread(() =>
				{
					ScanDir(fbd.SelectedPath, gameDB, this);

					this.Invoke(new Action(() =>
					{
						button1.Enabled = true;
						button2.Enabled = true;
						progressBar1.Style = ProgressBarStyle.Continuous;
						label1.Text = "Done";
					}));
				});
				thProcessing.Start();
			}

		}

		private void label1_Click(object sender, EventArgs e)
		{

		}

		public static uint ComputeCueCrc(CueSheet cue)
		{
			if (!cue.HasDataTrack())
			{
				throw new Exception("Audio only CDs are not supported for hashing");
			}

			var track = cue.FindFirstDataTrack();

			FileStream fs = new FileStream(track.FileName, FileMode.Open, FileAccess.Read);
			BinaryReader reader = new BinaryReader(fs);

			fs.Seek(track.FileOffset, SeekOrigin.Begin);

			int numsects = 20;
			uint crcAccum = 0xFFFFFFFF;

			for (int i = 0; i < numsects; ++i)
			{
				byte[] data;
				if (track.SectorSize == CueSheet.SectorSize._2352)
				{
					reader.ReadBytes(16);
					data = reader.ReadBytes(2048);
					reader.ReadBytes(288);
				}
				else
				{
					data = reader.ReadBytes(2048);
				}

				if (i == 0)  //check signature
				{
					if (data[0] != 0x82 || data[1] != 0xB1 || data[2] != 0x82 || data[3] != 0xCC || data[4] != 0x83 || data[5] != 0x76 || data[6] != 0x83 || data[7] != 0x8D)
					{
						//Debug.Assert(false);
						//Console.WriteLine("Err\n");
					}

				}

				crcAccum = Crc32.ComputeStep(crcAccum, data);
			}

			crcAccum = Crc32.Finalize(crcAccum);

			reader.Close();
			fs.Close();

			return crcAccum;
		}


		public static void DoScanWithoutUI(string dir)
		{
			GameDB db = new GameDB();
			if (File.Exists("db.xml"))
				db.ReadXml("db.xml");

			Crc32.gen_crc_table();

			ScanDir(dir,db , null);
		}

		public static void ConvertImages()
		{
			Crc32.gen_crc_table();

			if (!Directory.Exists("TileCache"))
				Directory.CreateDirectory("TileCache");

			GameDB db = new GameDB();
			if (File.Exists("db.xml"))
				db.ReadXml("db.xml");

			foreach (var game in db.Game)
			{
				if (!game.IsScreenshotNull())
				{
					if (!String.IsNullOrEmpty(game.Screenshot))
					{
						var ss = game.Screenshot.Replace("\\", "/");
						if (!File.Exists(ss))
						{
							Console.WriteLine("File " +ss + " is missing");
							continue;
						}

						//String dst = "TileCache/" + game.Screenshot.Substring(game.Screenshot.LastIndexOfAny(new char[] { '\\', '/' }) + 1);
						string dst = "TileCache/" + game.Screenshot.Replace("\\", "_").Replace("/", "_").Replace(":", "_");

						dst = dst.Replace(".png", ".tile");

						if (!File.Exists(dst) || File.GetLastWriteTime(game.Screenshot) > File.GetLastWriteTime(dst))
						{
							if (!Quantizer.Program.Quantize(ss, dst))
							{
								Console.WriteLine("Error converting " + game.Screenshot + ". Skipped");
							}
						}
					}
				}
			}
		}

		private void GameDBMgr_Shown(object sender, EventArgs e)
		{
			var c = dataGridView1.Columns[2];
			
			dataGridView1.Sort(dataGridView1.Columns[0], ListSortDirection.Ascending);

			gameBindingSource.ResetBindings(true);
		}

		private void genreBindingSource_CurrentChanged(object sender, EventArgs e)
		{

		}
	}

	public sealed class Crc32 : HashAlgorithm
	{
		public const UInt32 DefaultPolynomial = 0xedb88320u;
		public const UInt32 DefaultSeed = 0xffffffffu;

		static UInt32[] defaultTable;

		readonly UInt32 seed;
		readonly UInt32[] table;
		UInt32 hash;

		public Crc32()
			: this(DefaultPolynomial, DefaultSeed)
		{
		}

		public Crc32(UInt32 polynomial, UInt32 seed)
		{
			table = InitializeTable(polynomial);
			this.seed = hash = seed;
		}

		public override void Initialize()
		{
			hash = seed;
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize)
		{
			hash = CalculateHash(table, hash, array, ibStart, cbSize);
		}

		protected override byte[] HashFinal()
		{
			var hashBuffer = UInt32ToBigEndianBytes(~hash);
			HashValue = hashBuffer;
			return hashBuffer;
		}

		public override int HashSize { get { return 32; } }

		public static UInt32 Compute(byte[] buffer)
		{
			return Compute(DefaultSeed, buffer);
		}

		public static UInt32 Compute(UInt32 seed, byte[] buffer)
		{
			return Compute(DefaultPolynomial, seed, buffer);
		}

		public static UInt32 ComputeStep(UInt32 seed, byte[] buffer)
		{
			return CalculateHash(InitializeTable(DefaultPolynomial), seed, buffer, 0, buffer.Length);
		}

		public static UInt32 Finalize(UInt32 seed)
		{
			return ~seed;
		}


		public static UInt32 Compute(UInt32 polynomial, UInt32 seed, byte[] buffer)
		{
			return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
		}

		static UInt32[] InitializeTable(UInt32 polynomial)
		{
			if (polynomial == DefaultPolynomial && defaultTable != null)
				return defaultTable;

			var createTable = new UInt32[256];
			for (var i = 0; i < 256; i++)
			{
				var entry = (UInt32)i;
				for (var j = 0; j < 8; j++)
					if ((entry & 1) == 1)
						entry = (entry >> 1) ^ polynomial;
					else
						entry = entry >> 1;
				createTable[i] = entry;
			}

			if (polynomial == DefaultPolynomial)
				defaultTable = createTable;

			return createTable;
		}

		static UInt32 CalculateHash(UInt32[] table, UInt32 seed, IList<byte> buffer, int start, int size)
		{
			var hash = seed;
			for (var i = start; i < start + size; i++)
				hash = (hash >> 8) ^ table[buffer[i] ^ hash & 0xff];
			return hash;
		}

		static byte[] UInt32ToBigEndianBytes(UInt32 uint32)
		{
			var result = BitConverter.GetBytes(uint32);

			if (BitConverter.IsLittleEndian)
				Array.Reverse(result);

			return result;
		}

		//This is the same calculation than the HW does
		static uint[] crc_table = new uint[256];
		static public void gen_crc_table()
		{
			ushort i, j;
			uint crc_accum;

			for (i = 0; i < 256; i++)
			{
				crc_accum = ((uint)i << 24);
				for (j = 0; j < 8; j++)
				{
					if ((crc_accum & 0x80000000L) != 0)
						crc_accum = (crc_accum << 1) ^ 0x04c11db7;
					else
						crc_accum = (crc_accum << 1);
				}
				crc_table[i] = crc_accum;
			}
		}

		static public uint update_crc(uint crc_accum, byte[] data_blk_ptr, uint data_blk_size)
		{
			uint i, j;

			for (j = 0; j < data_blk_size; j++)
			{
				i = ((uint)(crc_accum >> 24) ^ data_blk_ptr[j ^ 3]) & 0xFF;
				crc_accum = (crc_accum << 8) ^ crc_table[i];
			}
			//crc_accum = ~crc_accum;

			return crc_accum;
		}

	}

	public class CueSheet
	{
		public enum TrackType { AUDIO, DATA, OTHER };
		public enum SectorSize { _2352, _2048, OTHER };
		public struct Track
		{
			public String FileName;
			public uint LBA;
			public uint LBAEnd;
			public uint Pregap;
			public uint FileOffset;
			public TrackType Type;
			public SectorSize SectorSize;
		}

		public Track []Tracks;

		uint ParseMSF(String msf)
		{
			string[] tokens = msf.Split(':');

			return (uint.Parse(tokens[0]) * 60 * 75) + uint.Parse(tokens[1]) * 75 + uint.Parse(tokens[2]);
		}

		public CueSheet(String file)
		{
			Tracks = new Track[100];

			String []lines = File.ReadAllLines(file);

			uint currentLBA = 0;
			uint currentOffset = 0;
			int currentTrack = -1;
			uint pregaps = 0;
			bool hasIndex0 = false;
			String fileName = "";
			FileInfo finfo = null;



			foreach (var line in lines)
			{
				String[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				if (tokens.Length < 1)
					continue;

				switch (tokens[0])
				{
					case "FILE":
						if (currentTrack != -1)	//terminate current track
						{
							uint secsz = (uint)(Tracks[currentTrack].SectorSize == SectorSize._2048 ? 2048 : 2352);
							uint totalframes = (uint)finfo.Length / secsz;

							currentLBA += totalframes;

							Tracks[currentTrack].LBAEnd = currentLBA;
						}

						//if it starts with ", parse till next "
						if (tokens[1][0] == '\"')
						{
							int start = line.IndexOf('\"');
							int end = line.IndexOf('\"',start + 1);
							fileName = line.Substring(start + 1, end - start - 1);
						}
						else
							fileName = tokens[1];

						fileName = Path.GetDirectoryName(file) + "/" + fileName;

						finfo = new FileInfo(fileName);

						currentOffset = 0;
						pregaps = 0;
						hasIndex0 = false;

						break;
					case "TRACK":
						int prvTrack = currentTrack;


						currentTrack = int.Parse(tokens[1]) - 1;

						Tracks[currentTrack].FileName = fileName;
						Tracks[currentTrack].FileOffset = currentOffset;
						Tracks[currentTrack].LBA = currentLBA;
						Tracks[currentTrack].Pregap = 0;
						Tracks[currentTrack].LBAEnd = 0;

						switch (tokens[2])   //format
						{
							case "AUDIO":
								Tracks[currentTrack].SectorSize = SectorSize._2352;
								Tracks[currentTrack].Type = TrackType.AUDIO;
								break;
							case "MODE1/2352":
								Tracks[currentTrack].SectorSize = SectorSize._2352;
								Tracks[currentTrack].Type = TrackType.DATA;
								break;
							case "MODE1/2048":
								Tracks[currentTrack].SectorSize = SectorSize._2048;
								Tracks[currentTrack].Type = TrackType.DATA;
								break;
							default:
								Tracks[currentTrack].SectorSize = SectorSize.OTHER;
								Tracks[currentTrack].Type = TrackType.OTHER;
								break;
						}

						if (prvTrack != -1)
						{
							Tracks[prvTrack].LBAEnd = Tracks[currentTrack].LBA;
						}

						break;
					case "PREGAP":
						Tracks[currentTrack].Pregap = ParseMSF(tokens[1]);
						currentLBA += Tracks[currentTrack].Pregap;
						pregaps += Tracks[currentTrack].Pregap;
						break;
					case "INDEX":

						switch (int.Parse(tokens[1]))
						{
							case 0: //index 0, pregap
								Tracks[currentTrack].Pregap = ParseMSF(tokens[2]);
								if (Tracks[currentTrack].Pregap > 500)   //bleh ??
								{
									currentLBA = ParseMSF(tokens[2]);
									hasIndex0 = true;
								}
								else
								{
									currentLBA += Tracks[currentTrack].Pregap;
								}


								break;
							case 1: //index 1, data
								uint offset = ParseMSF(tokens[2]);
								uint secsz = (uint)(Tracks[currentTrack].SectorSize == SectorSize._2048 ? 2048 : 2352);

								if (hasIndex0)
								{
									Tracks[currentTrack].LBA = offset + pregaps;
									Tracks[currentTrack].Pregap = offset - currentLBA;
									Tracks[currentTrack].FileOffset = offset * secsz;
									currentLBA = offset;
								}
								else
								{
									Tracks[currentTrack].LBA = currentLBA + offset;
									Tracks[currentTrack].FileOffset = offset * secsz;
								}
								if (currentTrack != 0)
									Tracks[currentTrack - 1].LBAEnd = Tracks[currentTrack].LBA;

								break;
						}


						break;

				}
			}

			if (currentTrack != -1) //terminate last track
			{
				uint secsz = (uint)(Tracks[currentTrack].SectorSize == SectorSize._2048 ? 2048 : 2352);
				uint totalframes = (uint)finfo.Length / secsz;

				currentLBA += totalframes;

				Tracks[currentTrack].LBAEnd = currentLBA;
			}


			int numTracks = currentTrack + 1;

			Track[] finalTracks = new Track[numTracks];

			Array.Copy(Tracks, finalTracks, numTracks);

			Tracks = finalTracks;

		}

		public bool HasDataTrack()
		{
			return Tracks.Count(x => x.Type == TrackType.DATA) != 0;
		}
		public Track FindFirstDataTrack()
		{
			return Tracks.FirstOrDefault(x => x.Type == TrackType.DATA);
		}
	}
}
