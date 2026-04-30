// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

using System.Collections;
using System.ComponentModel;
using System.Windows.Xps.Serialization.RCW;

namespace System.Windows.Xps.Packaging
{
    public enum PackageInterleavingOrder
    {
        None = 0,
        ResourceFirst = 1,
        ResourceLast = 2,
        ImagesLast = 3,
    }

    public enum PackagingAction
    {
        None = 0,
        AddingDocumentSequence = 1,
        DocumentSequenceCompleted = 2,
        AddingFixedDocument = 3,
        FixedDocumentCompleted = 4,
        AddingFixedPage = 5,
        FixedPageCompleted = 6,
        ResourceAdded = 7,
        FontAdded = 8,
        ImageAdded = 9,
        XpsDocumentCommitted = 10,
    }

    public delegate void PackagingProgressEventHandler(object sender, PackagingProgressEventArgs e);
}

namespace System.Windows.Xps.Serialization.RCW
{
    internal partial interface IPrintDocumentPackageTarget
    {
        void Cancel();
    }

    internal partial interface IXpsDocumentPackageTarget
    {
    }

    internal class PrintDocumentPackageStatusProvider
    {
        public PrintDocumentPackageStatusProvider(IPrintDocumentPackageTarget docPackageTarget)
        {
        }

        public System.Threading.ManualResetEvent JobIdAcquiredEvent => null;

        public int JobId => 0;
    }
}

namespace System.Windows.Xps.Serialization
{
    internal delegate void XpsSerializationXpsDriverDocEventHandler(object sender, XpsSerializationXpsDriverDocEventArgs e);

    public abstract class PackageSerializationManager : IDisposable
    {
        protected PackageSerializationManager()
        {
        }

        public abstract void SaveAsXaml(object serializedObject);

        internal int JobIdentifier { get; set; }

        void IDisposable.Dispose()
        {
        }
    }

    public abstract class BasePackagingPolicy : IDisposable
    {
        protected BasePackagingPolicy()
        {
        }

        public abstract System.Xml.XmlWriter AcquireXmlWriterForFixedDocumentSequence();
        public abstract void ReleaseXmlWriterForFixedDocumentSequence();
        public abstract System.Xml.XmlWriter AcquireXmlWriterForFixedDocument();
        public abstract void ReleaseXmlWriterForFixedDocument();
        public abstract System.Xml.XmlWriter AcquireXmlWriterForFixedPage();
        public abstract void ReleaseXmlWriterForFixedPage();
        public abstract void RelateResourceToCurrentPage(Uri targetUri, string relationshipName);
        public abstract void RelateRestrictedFontToCurrentDocument(Uri targetUri);
        public abstract void PersistPrintTicket(System.Printing.PrintTicket printTicket);
        public abstract System.Xml.XmlWriter AcquireXmlWriterForPage();
        public abstract System.Xml.XmlWriter AcquireXmlWriterForResourceDictionary();
        public abstract System.Collections.Generic.IList<string> AcquireStreamForLinkTargets();
        public abstract void PreCommitCurrentPage();
        public abstract XpsResourceStream AcquireResourceStreamForXpsFont();
        public abstract XpsResourceStream AcquireResourceStreamForXpsFont(string resourceId);
        public abstract void ReleaseResourceStreamForXpsFont();
        public abstract void ReleaseResourceStreamForXpsFont(string resourceId);
        public abstract XpsResourceStream AcquireResourceStreamForXpsImage(string resourceId);
        public abstract void ReleaseResourceStreamForXpsImage();
        public abstract XpsResourceStream AcquireResourceStreamForXpsColorContext(string resourceId);
        public abstract void ReleaseResourceStreamForXpsColorContext();
        public abstract XpsResourceStream AcquireResourceStreamForXpsResourceDictionary(string resourceId);
        public abstract void ReleaseResourceStreamForXpsResourceDictionary();
        public abstract Uri CurrentFixedDocumentUri { get; }
        public abstract Uri CurrentFixedPageUri { get; }

        void IDisposable.Dispose()
        {
        }
    }

    public enum PrintTicketLevel
    {
        None = 0,
        FixedDocumentSequencePrintTicket = 1,
        FixedDocumentPrintTicket = 2,
        FixedPagePrintTicket = 3,
    }

    public enum FontSubsetterCommitPolicies
    {
        None = 0,
        CommitPerPage = 1,
        CommitPerDocument = 2,
        CommitEntireSequence = 3,
    }

    public enum XpsWritingProgressChangeLevel
    {
        None = 0,
        FixedDocumentSequenceWritingProgress = 1,
        FixedDocumentWritingProgress = 2,
        FixedPageWritingProgress = 3,
    }

    public sealed class XpsSerializationCompletedEventArgs : AsyncCompletedEventArgs
    {
        public XpsSerializationCompletedEventArgs(bool canceled, object state, Exception exception)
            : base(exception, canceled, state)
        {
        }
    }

    public delegate void XpsSerializationCompletedEventHandler(object sender, XpsSerializationCompletedEventArgs e);

    public class XpsSerializationPrintTicketRequiredEventArgs : EventArgs
    {
        public XpsSerializationPrintTicketRequiredEventArgs(PrintTicketLevel printTicketLevel, int sequence)
        {
            PrintTicketLevel = printTicketLevel;
            Sequence = sequence;
        }

        public bool Modified { get; internal set; }

        public System.Printing.PrintTicket PrintTicket { get; set; }

        public PrintTicketLevel PrintTicketLevel { get; }

        public int Sequence { get; }
    }

    public delegate void XpsSerializationPrintTicketRequiredEventHandler(object sender, XpsSerializationPrintTicketRequiredEventArgs e);

    public sealed class XpsSerializationProgressChangedEventArgs : ProgressChangedEventArgs
    {
        public XpsSerializationProgressChangedEventArgs(XpsWritingProgressChangeLevel writingLevel, int pageNumber, int progressPercentage, object userToken)
            : base(progressPercentage, userToken)
        {
            WritingLevel = writingLevel;
            PageNumber = pageNumber;
        }

        public int PageNumber { get; }

        public XpsWritingProgressChangeLevel WritingLevel { get; }
    }

    public delegate void XpsSerializationProgressChangedEventHandler(object sender, XpsSerializationProgressChangedEventArgs e);

    public class XpsPackagingPolicy : BasePackagingPolicy
    {
        public XpsPackagingPolicy(System.Windows.Xps.Packaging.XpsDocument xpsPackage)
        {
        }

        public XpsPackagingPolicy(System.Windows.Xps.Packaging.XpsDocument xpsPackage, System.Windows.Xps.Packaging.PackageInterleavingOrder interleavingType)
        {
        }

        public event System.Windows.Xps.Packaging.PackagingProgressEventHandler PackagingProgressEvent
        {
            add { }
            remove { }
        }

        public override Uri CurrentFixedDocumentUri => throw null;

        public override Uri CurrentFixedPageUri => throw null;

        public override XpsResourceStream AcquireResourceStreamForXpsColorContext(string resourceId) => throw null;
        public override XpsResourceStream AcquireResourceStreamForXpsFont() => throw null;
        public override XpsResourceStream AcquireResourceStreamForXpsFont(string resourceId) => throw null;
        public override XpsResourceStream AcquireResourceStreamForXpsImage(string resourceId) => throw null;
        public override XpsResourceStream AcquireResourceStreamForXpsResourceDictionary(string resourceId) => throw null;
        public override System.Collections.Generic.IList<string> AcquireStreamForLinkTargets() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForFixedDocument() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForFixedDocumentSequence() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForFixedPage() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForPage() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForResourceDictionary() => throw null;
        public override void PersistPrintTicket(System.Printing.PrintTicket printTicket) { }
        public override void PreCommitCurrentPage() { }
        public override void RelateResourceToCurrentPage(Uri targetUri, string relationshipName) { }
        public override void RelateRestrictedFontToCurrentDocument(Uri targetUri) { }
        public override void ReleaseResourceStreamForXpsColorContext() { }
        public override void ReleaseResourceStreamForXpsFont() { }
        public override void ReleaseResourceStreamForXpsFont(string resourceId) { }
        public override void ReleaseResourceStreamForXpsImage() { }
        public override void ReleaseResourceStreamForXpsResourceDictionary() { }
        public override void ReleaseXmlWriterForFixedDocument() { }
        public override void ReleaseXmlWriterForFixedDocumentSequence() { }
        public override void ReleaseXmlWriterForFixedPage() { }
    }

    internal class XpsOMPackagingPolicy : BasePackagingPolicy
    {
        internal XpsOMPackagingPolicy(IXpsDocumentPackageTarget packageTarget)
        {
        }

        public bool IsValid => true;

        public object PrintQueueReference
        {
            set { }
        }

        public override Uri CurrentFixedDocumentUri => throw null;

        public override Uri CurrentFixedPageUri => throw null;

        public override XpsResourceStream AcquireResourceStreamForXpsColorContext(string resourceId) => throw null;
        public override XpsResourceStream AcquireResourceStreamForXpsFont() => throw null;
        public override XpsResourceStream AcquireResourceStreamForXpsFont(string resourceId) => throw null;
        public override XpsResourceStream AcquireResourceStreamForXpsImage(string resourceId) => throw null;
        public override XpsResourceStream AcquireResourceStreamForXpsResourceDictionary(string resourceId) => throw null;
        public override System.Collections.Generic.IList<string> AcquireStreamForLinkTargets() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForFixedDocument() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForFixedDocumentSequence() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForFixedPage() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForPage() => throw null;
        public override System.Xml.XmlWriter AcquireXmlWriterForResourceDictionary() => throw null;
        public override void PersistPrintTicket(System.Printing.PrintTicket printTicket) { }
        public override void PreCommitCurrentPage() { }
        public override void RelateResourceToCurrentPage(Uri targetUri, string relationshipName) { }
        public override void RelateRestrictedFontToCurrentDocument(Uri targetUri) { }
        public override void ReleaseResourceStreamForXpsColorContext() { }
        public override void ReleaseResourceStreamForXpsFont() { }
        public override void ReleaseResourceStreamForXpsFont(string resourceId) { }
        public override void ReleaseResourceStreamForXpsImage() { }
        public override void ReleaseResourceStreamForXpsResourceDictionary() { }
        public override void ReleaseXmlWriterForFixedDocument() { }
        public override void ReleaseXmlWriterForFixedDocumentSequence() { }
        public override void ReleaseXmlWriterForFixedPage() { }
    }

    public class XpsResourceStream
    {
        public XpsResourceStream(System.IO.Stream stream, Uri uri)
        {
            Stream = stream;
            Uri = uri;
        }

        public System.IO.Stream Stream { get; }

        public Uri Uri { get; }

        public void Initialize()
        {
        }
    }

    public class XpsSerializationManager : PackageSerializationManager
    {
        public XpsSerializationManager(BasePackagingPolicy packagingPolicy, bool batchMode)
        {
            IsBatchMode = batchMode;
        }

        public bool IsBatchMode { get; }

        public event XpsSerializationPrintTicketRequiredEventHandler XpsSerializationPrintTicketRequired
        {
            add { }
            remove { }
        }

        public event XpsSerializationProgressChangedEventHandler XpsSerializationProgressChanged
        {
            add { }
            remove { }
        }

        internal event XpsSerializationXpsDriverDocEventHandler XpsSerializationXpsDriverDocEvent
        {
            add { }
            remove { }
        }

        public virtual void Commit()
        {
        }

        public override void SaveAsXaml(object serializedObject)
        {
        }

        public void SetFontSubsettingCountPolicy(int countPolicy)
        {
        }

        public void SetFontSubsettingPolicy(FontSubsetterCommitPolicies policy)
        {
        }

        internal static bool IsSerializedObjectTypeSupported(object serializedObject, bool isBatchMode)
        {
            return true;
        }
    }

    public sealed class XpsSerializationManagerAsync : XpsSerializationManager
    {
        public XpsSerializationManagerAsync(BasePackagingPolicy packagingPolicy, bool batchMode)
            : base(packagingPolicy, batchMode)
        {
        }

        public event XpsSerializationCompletedEventHandler XpsSerializationCompleted
        {
            add { }
            remove { }
        }

        public void CancelAsync()
        {
        }

        public override void Commit()
        {
        }

        public override void SaveAsXaml(object serializedObject)
        {
        }
    }

    internal class XpsOMSerializationManager : PackageSerializationManager
    {
        public XpsOMSerializationManager(XpsOMPackagingPolicy xpsOMManager, bool batchMode)
        {
        }

        internal event XpsSerializationPrintTicketRequiredEventHandler XpsSerializationPrintTicketRequired
        {
            add { }
            remove { }
        }

        internal event XpsSerializationProgressChangedEventHandler XpsSerializationProgressChanged
        {
            add { }
            remove { }
        }

        internal virtual void Commit()
        {
        }

        public override void SaveAsXaml(object serializedObject)
        {
        }
    }

    internal class XpsOMSerializationManagerAsync : XpsOMSerializationManager
    {
        public XpsOMSerializationManagerAsync(XpsOMPackagingPolicy packagingPolicy, bool batchMode)
            : base(packagingPolicy, batchMode)
        {
        }

        public event XpsSerializationCompletedEventHandler XpsSerializationCompleted
        {
            add { }
            remove { }
        }

        public void CancelAsync()
        {
        }

        internal override void Commit()
        {
        }

        public override void SaveAsXaml(object serializedObject)
        {
        }
    }

    internal sealed class NgcSerializationManager : PackageSerializationManager
    {
        public NgcSerializationManager(object queue, bool isBatchMode)
        {
        }

        public event XpsSerializationPrintTicketRequiredEventHandler XpsSerializationPrintTicketRequired
        {
            add { }
            remove { }
        }

        public event XpsSerializationProgressChangedEventHandler XpsSerializationProgressChanged
        {
            add { }
            remove { }
        }

        public void Cancel()
        {
        }

        public void Commit()
        {
        }

        public override void SaveAsXaml(object serializedObject)
        {
        }
    }

    internal sealed class NgcSerializationManagerAsync : PackageSerializationManager
    {
        public NgcSerializationManagerAsync(object queue, bool isBatchMode)
        {
        }

        public event XpsSerializationCompletedEventHandler XpsSerializationCompleted
        {
            add { }
            remove { }
        }

        public event XpsSerializationPrintTicketRequiredEventHandler XpsSerializationPrintTicketRequired
        {
            add { }
            remove { }
        }

        public event XpsSerializationProgressChangedEventHandler XpsSerializationProgressChanged
        {
            add { }
            remove { }
        }

        public void CancelAsync()
        {
        }

        public void Commit()
        {
        }

        public override void SaveAsXaml(object serializedObject)
        {
        }
    }

    internal sealed class MXDWSerializationManager
    {
        public MXDWSerializationManager(object queue)
        {
        }

        public bool IsPassThruSupported => false;

        public string MxdwFileName => string.Empty;

        public void Commit()
        {
        }

        public void EnablePassThru()
        {
        }
    }
}
