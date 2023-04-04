using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using Wox.Plugin;

namespace Wox.Previewer.OfficePreview
{
    public class ExplorerPreviewViewModel : BaseModel
    {
        internal const string GUID_ISHELLITEM = "69677D6E-80E3-4405-9C48-AE190D396C44";

        #region P/Invoke
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc, [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem ppv
        );
        #endregion

        private object mCurrentPreviewHandler;
        private Guid mCurrentPreviewHandlerGUID;
        private Stream mCurrentPreviewHandlerStream;

        public WindowsFormsHost WindowHost { get; set; }

        public Grid MainGrid { get; set; }

        public ExplorerPreviewViewModel()
        {
            WindowHost = new NoFlickerWindowsFormsHost();
        }

        public void LoadFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;
            UnloadPreviewHandler();

            // try to get GUID for the preview handler
            Guid guid = GetPreviewHandlerGUID(filePath);
            var errorMessage = "";
            if (guid != Guid.Empty)
            {
                try
                {
                    mCurrentPreviewHandlerGUID = guid;

                    // need to instantiate a different COM type (file format has changed)
                    if (mCurrentPreviewHandler != null) Marshal.FinalReleaseComObject(mCurrentPreviewHandler);

                    // use reflection to instantiate the preview handler type
                    Type comType = Type.GetTypeFromCLSID(mCurrentPreviewHandlerGUID);
                    mCurrentPreviewHandler = Activator.CreateInstance(comType);


                    if (mCurrentPreviewHandler is IInitializeWithFile initializeWithFile)
                    {
                        // some handlers accept a filename
                        initializeWithFile.Initialize(filePath, 0);
                    }
                    else if (mCurrentPreviewHandler is IInitializeWithStream initializeWithStream)
                    {
                        // other handlers want an IStream (in this case, a file stream)
                        mCurrentPreviewHandlerStream = File.OpenRead(filePath);
                        StreamWrapper stream = new StreamWrapper(mCurrentPreviewHandlerStream);
                        initializeWithStream.Initialize(stream, 0);
                    }
                    else if (mCurrentPreviewHandler is IInitializeWithItem initializeWithItem)
                    {
                        // a third category exists, must be initialized with a shell item
                        IShellItem shellItem;
                        SHCreateItemFromParsingName(filePath, IntPtr.Zero, new Guid(GUID_ISHELLITEM), out shellItem);
                        initializeWithItem.Initialize(shellItem, 0);
                    }

                    if (mCurrentPreviewHandler is IPreviewHandler previewHandler)
                    {
                        var control = new PreviewControl();
                        control.Size = new Size(DoubleToInt(MainGrid.ActualWidth), DoubleToInt(MainGrid.ActualHeight));
                        WindowHost.Child = control;
                        // bind the preview handler to the control's bounds and preview the content
                        Rectangle r = control.ClientRectangle;
                        previewHandler.SetWindow(control.Handle, ref r);
                        var result = previewHandler.DoPreview();
                        if (result != HRESULT.S_OK)
                        {
                            errorMessage = "Preview could not be generated.\n" + result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = "Preview could not be generated.\n" + ex.Message;
                }
            }
            else
            {
                errorMessage = "No preview available.";
            }
            Debug.WriteLine(errorMessage);
        }

        public void Unload()
        {
            UnloadPreviewHandler();
        }

        public void OnSizeChanged(double width, double height)
        {
            if (mCurrentPreviewHandler is IPreviewHandler preview && WindowHost.Child != null)
            {
                WindowHost.Child.Size = new Size(DoubleToInt(width), DoubleToInt(height));
                var r = WindowHost.Child.ClientRectangle;
                preview.SetRect(ref r);
            }
        }

        public void UnloadPreviewHandler()
        {
            if (mCurrentPreviewHandler is IPreviewHandler previewHandler)
            {
                // explicitly unload the content
                try
                {
                    previewHandler.Unload();
                }
                finally
                {
                    mCurrentPreviewHandler = null;
                }
            }


            if (mCurrentPreviewHandlerStream != null)
            {
                mCurrentPreviewHandlerStream.Dispose();
                mCurrentPreviewHandlerStream = null;
            }

            if (WindowHost?.Child != null)
            {
                WindowHost.Child.Dispose();
            }
        }

        public static Guid GetPreviewHandlerGUID(string filename)
        {
            // open the registry key corresponding to the file extension
            RegistryKey ext = Registry.ClassesRoot.OpenSubKey(Path.GetExtension(filename));
            if (ext != null)
            {
                // open the key that indicates the GUID of the preview handler type
                RegistryKey test = ext.OpenSubKey("shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}");
                if (test != null) return new Guid(Convert.ToString(test.GetValue(null)));

                // sometimes preview handlers are declared on key for the class
                string className = Convert.ToString(ext.GetValue(null));
                if (className != null)
                {
                    test = Registry.ClassesRoot.OpenSubKey(className + "\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}");
                    if (test != null)
                        return new Guid(Convert.ToString(test.GetValue(null)));
                    else
                    {
                        var OpenWithProgIds = ext.OpenSubKey("OpenWithProgIds");
                        if (OpenWithProgIds != null)
                        {
                            var values = OpenWithProgIds.GetValueNames();
                            if (values.Length > 0)
                            {
                                var alternate = Registry.ClassesRoot.OpenSubKey(values[0] + "\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}");
                                if (alternate != null)
                                    return new Guid(Convert.ToString(alternate.GetValue(null)));
                            }
                        }
                    }
                }
            }

            return Guid.Empty;
        }

        private int DoubleToInt(double value)
        {
            return Convert.ToInt32(Math.Floor(value));
        }
    }

    #region COM Interlope

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("8895b1c6-b41f-4c1c-a562-0d564250836f")]
    internal interface IPreviewHandler
    {
        void SetWindow(IntPtr hwnd, ref Rectangle rect);
        void SetRect(ref Rectangle rect);
        HRESULT DoPreview();
        void Unload();
        void SetFocus();
        void QueryFocus(out IntPtr phwnd);
        [PreserveSig]
        uint TranslateAccelerator(ref Message pmsg);
    }

    enum HRESULT
    {
        S_OK,//The operation completed successfully.
        E_PREVIEWHANDLER_DRM_FAIL,//Blocked by digital rights management.
        E_PREVIEWHANDLER_NOAUTH,//Blocked by file permissions.
        E_PREVIEWHANDLER_NOTFOUND,//Item was not found.
        E_PREVIEWHANDLER_CORRUPT,//Item was corrupt.
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("b7d14566-0509-4cce-a71f-0a554233bd9b")]
    internal interface IInitializeWithFile
    {
        void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("b824b49d-22ac-4161-ac8a-9916e8fa3f7f")]
    internal interface IInitializeWithStream
    {
        void Initialize(IStream pstream, uint grfMode);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("7F73BE3F-FB79-493C-A6C7-7EE14E245841")]
    interface IInitializeWithItem
    {
        void Initialize(IShellItem psi, uint grfMode);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid(ExplorerPreviewViewModel.GUID_ISHELLITEM)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    };

    /// <summary>
    /// Provides a bare-bones implementation of System.Runtime.InteropServices.IStream that wraps an System.IO.Stream.
    /// </summary>
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    internal class StreamWrapper : IStream
    {

        private System.IO.Stream mInner;

        /// <summary>
        /// Initialises a new instance of the StreamWrapper class, using the specified System.IO.Stream.
        /// </summary>
        /// <param name="inner"></param>
        public StreamWrapper(System.IO.Stream inner)
        {
            mInner = inner;
        }

        /// <summary>
        /// This operation is not supported.
        /// </summary>
        /// <param name="ppstm"></param>
        public void Clone(out IStream ppstm)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This operation is not supported.
        /// </summary>
        /// <param name="grfCommitFlags"></param>
        public void Commit(int grfCommitFlags)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This operation is not supported.
        /// </summary>
        /// <param name="pstm"></param>
        /// <param name="cb"></param>
        /// <param name="pcbRead"></param>
        /// <param name="pcbWritten"></param>
        public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This operation is not supported.
        /// </summary>
        /// <param name="libOffset"></param>
        /// <param name="cb"></param>
        /// <param name="dwLockType"></param>
        public void LockRegion(long libOffset, long cb, int dwLockType)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Reads a sequence of bytes from the underlying System.IO.Stream.
        /// </summary>
        /// <param name="pv"></param>
        /// <param name="cb"></param>
        /// <param name="pcbRead"></param>
        public void Read(byte[] pv, int cb, IntPtr pcbRead)
        {
            int bytesRead = mInner.Read(pv, 0, cb);
            if (pcbRead != IntPtr.Zero) Marshal.WriteInt32(pcbRead, bytesRead);
        }

        /// <summary>
        /// This operation is not supported.
        /// </summary>
        public void Revert()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Advances the stream to the specified position.
        /// </summary>
        /// <param name="dlibMove"></param>
        /// <param name="dwOrigin"></param>
        /// <param name="plibNewPosition"></param>
        public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
        {
            int pos = (int)mInner.Seek(dlibMove, (System.IO.SeekOrigin)dwOrigin);
            if (plibNewPosition != IntPtr.Zero) Marshal.WriteInt32(plibNewPosition, pos);
        }

        /// <summary>
        /// This operation is not supported.
        /// </summary>
        /// <param name="libNewSize"></param>
        public void SetSize(long libNewSize)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Returns details about the stream, including its length, type and name.
        /// </summary>
        /// <param name="pstatstg"></param>
        /// <param name="grfStatFlag"></param>
        public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag)
        {
            pstatstg = new System.Runtime.InteropServices.ComTypes.STATSTG();
            pstatstg.cbSize = mInner.Length;
            pstatstg.type = 2; // stream type
            pstatstg.pwcsName = (mInner is FileStream) ? ((FileStream)mInner).Name : String.Empty;
        }

        /// <summary>
        /// This operation is not supported.
        /// </summary>
        /// <param name="libOffset"></param>
        /// <param name="cb"></param>
        /// <param name="dwLockType"></param>
        public void UnlockRegion(long libOffset, long cb, int dwLockType)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes a sequence of bytes to the underlying System.IO.Stream.
        /// </summary>
        /// <param name="pv"></param>
        /// <param name="cb"></param>
        /// <param name="pcbWritten"></param>
        public void Write(byte[] pv, int cb, IntPtr pcbWritten)
        {
            mInner.Write(pv, 0, cb);
            if (pcbWritten != IntPtr.Zero) Marshal.WriteInt32(pcbWritten, (Int32)cb);
        }
    }

    #endregion

}
