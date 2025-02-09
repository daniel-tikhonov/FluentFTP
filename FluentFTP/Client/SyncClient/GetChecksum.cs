using System;
using FluentFTP.Helpers;
using FluentFTP.Helpers.Hashing;
using HashAlgos = FluentFTP.Helpers.Hashing.HashAlgorithms;
using System.Threading;
using System.Threading.Tasks;

namespace FluentFTP {
	public partial class FtpClient {


		#region Checksum

		/// <summary>
		/// Retrieves a checksum of the given file using the specified checksum algorithm, or using the first available algorithm that the server supports.
		/// </summary>
		/// <remarks>
		/// The algorithm used goes in this order:
		/// 1. HASH command using the first supported algorithm.
		/// 2. MD5 / XMD5 / MMD5 commands
		/// 3. XSHA1 command
		/// 4. XSHA256 command
		/// 5. XSHA512 command
		/// 6. XCRC command
		/// </remarks>
		/// <param name="path">Full or relative path of the file to checksum</param>
		/// <param name="algorithm">Specify an algorithm that you prefer, or NONE to use the first available algorithm. If the preferred algorithm is not supported, a blank hash is returned.</param>
		/// <returns><see cref="FtpHash"/> object containing the value and algorithm. Use the <see cref="FtpHash.IsValid"/> property to
		/// determine if this command was successful. <see cref="FtpCommandException"/>s can be thrown from
		/// the underlying calls.</returns>
		/// <exception cref="FtpCommandException">The command fails</exception>
		public FtpHash GetChecksum(string path, FtpHashAlgorithm algorithm = FtpHashAlgorithm.NONE) {

			if (path == null) {
				throw new ArgumentException("Required argument is null", nameof(path));
			}

			ValidateChecksumAlgorithm(algorithm);

			path = path.GetFtpPath();

			LogFunction(nameof(GetChecksum), new object[] { path });

			var useFirst = (algorithm == FtpHashAlgorithm.NONE);

			// if HASH is supported and the caller prefers an algorithm and that algorithm is supported
			if (HasFeature(FtpCapability.HASH) && !useFirst && HashAlgorithms.HasFlag(algorithm)) {

				// switch to that algorithm
				SetHashAlgorithmInternal(algorithm);

				// get the hash of the file using HASH Command
				return HashCommandInternal(path);

			}

			// if HASH is supported and the caller does not prefer any specific algorithm
			else if (HasFeature(FtpCapability.HASH) && useFirst) {

				// switch to the first preferred algorithm
				SetHashAlgorithmInternal(HashAlgos.FirstSupported(HashAlgorithms));

				// get the hash of the file using HASH Command
				return HashCommandInternal(path);
			}
			else {
				var result = new FtpHash();

				// execute the first available algorithm, or the preferred algorithm if specified

				if (HasFeature(FtpCapability.MD5) && (useFirst || algorithm == FtpHashAlgorithm.MD5)) {
					result.Value = GetHashInternal(path, "MD5");
					result.Algorithm = FtpHashAlgorithm.MD5;
				}
				else if (HasFeature(FtpCapability.XMD5) && (useFirst || algorithm == FtpHashAlgorithm.MD5)) {
					result.Value = GetHashInternal(path, "XMD5");
					result.Algorithm = FtpHashAlgorithm.MD5;
				}
				else if (HasFeature(FtpCapability.MMD5) && (useFirst || algorithm == FtpHashAlgorithm.MD5)) {
					result.Value = GetHashInternal(path, "MMD5");
					result.Algorithm = FtpHashAlgorithm.MD5;
				}
				else if (HasFeature(FtpCapability.XSHA1) && (useFirst || algorithm == FtpHashAlgorithm.SHA1)) {
					result.Value = GetHashInternal(path, "XSHA1");
					result.Algorithm = FtpHashAlgorithm.SHA1;
				}
				else if (HasFeature(FtpCapability.XSHA256) && (useFirst || algorithm == FtpHashAlgorithm.SHA256)) {
					result.Value = GetHashInternal(path, "XSHA256");
					result.Algorithm = FtpHashAlgorithm.SHA256;
				}
				else if (HasFeature(FtpCapability.XSHA512) && (useFirst || algorithm == FtpHashAlgorithm.SHA512)) {
					result.Value = GetHashInternal(path, "XSHA512");
					result.Algorithm = FtpHashAlgorithm.SHA512;
				}
				else if (HasFeature(FtpCapability.XCRC) && (useFirst || algorithm == FtpHashAlgorithm.CRC)) {
					result.Value = GetHashInternal(path, "XCRC");
					result.Algorithm = FtpHashAlgorithm.CRC;
				}

				return result;
			}
		}

		#endregion

		#region MD5, SHA1, SHA256, SHA512 Commands

		/// <summary>
		/// Gets the hash of the specified file using the given command.
		/// </summary>
		internal string GetHashInternal(string path, string command) {
			FtpReply reply;
			string response;

			if (!(reply = Execute(command + " " + path)).Success) {
				throw new FtpCommandException(reply);
			}

			response = reply.Message;
			response = CleanHashResult(path, response);
			return response;
		}

		#endregion

		#region HASH Command

		/// <summary>
		/// Sets the hash algorithm on the server to use for the HASH command. 
		/// </summary>
		internal void SetHashAlgorithmInternal(FtpHashAlgorithm algorithm) {
			FtpReply reply;

			// skip setting the hash algo if the server is already configured to it
			if (Status.LastHashAlgo == algorithm) {
				return;
			}

			lock (m_lock) {
				if ((HashAlgorithms & algorithm) != algorithm) {
					throw new NotImplementedException("The hash algorithm " + algorithm.ToString() + " was not advertised by the server.");
				}

				string algoName = HashAlgos.PrintToString(algorithm);

				if (!(reply = Execute("OPTS HASH " + algoName)).Success) {
					throw new FtpCommandException(reply);
				}

				// save the current hash algo so no need to repeat this command
				Status.LastHashAlgo = algorithm;

			}
		}

		/// <summary>
		/// Gets the hash of an object on the server using the currently selected hash algorithm.
		/// </summary>
		protected FtpHash HashCommandInternal(string path) {
			FtpReply reply;

			lock (m_lock) {
				if (!(reply = Execute("HASH " + path)).Success) {
					throw new FtpCommandException(reply);
				}

			}

			// parse hash from the server reply
			return HashParser.Parse(reply.Message);
		}

		#endregion

	}
}
