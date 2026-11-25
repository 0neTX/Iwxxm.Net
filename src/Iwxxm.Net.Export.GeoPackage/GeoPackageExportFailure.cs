using System;
using System.IO;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// The one place that words a failed export and cleans up after it, shared by the exporter and the writer.
    /// </summary>
    internal static class GeoPackageExportFailure
    {
        internal const string Prefix = "The GeoPackage export failed: ";

        internal const string IncompatibleCoreText =
            "the installed Iwxxm.Net is not compatible with this version of Iwxxm.Net.Export.GeoPackage; " +
            "use the same version of both packages, the one this add-on was built against";

        /// <summary>A failed <see cref="Result{T}"/> with <see cref="GeoPackageErrorCodes.ExportError"/>, located at the document id.</summary>
        internal static Result<T> Create<T>(string documentId, string reason) =>
            Result<T>.Failure(GeoPackageErrorCodes.ExportError, Prefix + reason, ErrorCategory.DomainMapping, documentId);

        /// <summary>A failed <see cref="Result{T}"/> for <paramref name="exception"/>, worded by <see cref="Describe"/>.</summary>
        internal static Result<T> Create<T>(string documentId, Exception exception) =>
            Create<T>(documentId, Describe(exception));

        /// <summary>
        /// The reason text of a failure: the message of <paramref name="exception"/> and, when the innermost exception is a
        /// different one, its type and message in brackets, so that a native-library load failure (a
        /// <see cref="TypeInitializationException"/> around the real cause) can be diagnosed from the message alone. An
        /// access or missing-member failure is worded as a version mismatch between the add-on and <c>Iwxxm.Net</c>.
        /// </summary>
        internal static string Describe(Exception exception)
        {
            Exception innermost = exception.GetBaseException();
            string reason = IsCoreIncompatibility(exception) ? IncompatibleCoreText : exception.Message;

            bool differs = !ReferenceEquals(innermost, exception);
            if (differs || IsCoreIncompatibility(exception))
                reason += " [" + innermost.GetType().Name + ": " + innermost.Message + "]";

            return reason;
        }

        /// <summary>
        /// True when <paramref name="exception"/> or one of its inner exceptions says that a member or type of the core is
        /// not accessible or not there: the add-on reads internals of <c>Iwxxm.Net</c> (through
        /// <c>InternalsVisibleTo</c>), which only the version it was built against grants.
        /// </summary>
        internal static bool IsCoreIncompatibility(Exception? exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is MethodAccessException || current is FieldAccessException || current is TypeAccessException || current is MissingMemberException)
                    return true;
            }

            return false;
        }

        /// <summary>Deletes a file; a failed cleanup must not hide the outcome it follows.</summary>
        internal static void DeleteQuietly(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
