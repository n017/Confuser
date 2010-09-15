using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Mono.Cecil;

namespace Confuser
{
    class CultureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((string)value == string.Empty) return "null";
            else return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.ToString() == "null") return string.Empty;
            else return value;
        }
    }
    class ByteArrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || !(value is byte[]) || (value as byte[]).Length == 0)
                return "null";
            StringBuilder sb = new StringBuilder();
            foreach (byte i in value as byte[])
                sb.Append(i.ToString("x2"));
            return sb.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.ToString() == "null" || value == null) return new byte[0];
            List<byte> ret = new List<byte>();
            string str = value.ToString();
            for (int i = 0; i < str.Length; i += 2)
                ret.Add(System.Convert.ToByte(str.Substring(i, 2), 16));
            return ret.ToArray();
        }
    }
    class KindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (!(value is ModuleKind))
                return "Unknown";
            switch ((ModuleKind)value)
            {
                case ModuleKind.Console:
                    return "Console Application";
                case ModuleKind.Dll:
                    return "Class Library";
                case ModuleKind.NetModule:
                    return "Net Module(???)";
                case ModuleKind.Windows:
                    return "Windows Application";
                default:
                    return "Unknown";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch (value.ToString())
            {
                case "Console Application":
                    return ModuleKind.Console;
                case "Class Library":
                    return ModuleKind.Dll;
                case "Net Module(???)":
                    return ModuleKind.NetModule;
                case "Windows Application":
                    return ModuleKind.Windows;
                default:
                    return (ModuleKind)0;
            }
        }
    }
    static class Helper
    {

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);
        [DllImport("kernel32.dll")]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, int lpType);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LockResource(IntPtr hResData);
        [DllImport("kernel32.dll")]
        private static extern int SizeofResource(IntPtr hModule, IntPtr hResInfo);
        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, int lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Auto)]
        private delegate bool EnumResNameProc(IntPtr hModule, int lpszType, IntPtr lpszName, IntPtr lParam);

        public static BitmapImage GetIcon(string path)
        {
            IntPtr hMod = LoadLibraryEx(path, IntPtr.Zero, 0x00000002);
            MemoryStream mem = null;
            EnumResourceNames(hMod, 3 + 11, new EnumResNameProc(delegate(IntPtr hModule, int lpszType, IntPtr lpszName, IntPtr lParam)
            {
                if (lpszType == 3 + 11)
                {
                    IntPtr res = FindResource(hMod, lpszName, 3 + 11);
                    IntPtr dat = LoadResource(hMod, res);
                    IntPtr ptr = LockResource(dat);
                    int size = SizeofResource(hMod, res);
                    Console.WriteLine(ptr.ToString("X8"));
                    Console.WriteLine(size.ToString("X8"));
                    Console.WriteLine();
                    byte[] byteArr = new byte[size];
                    Marshal.Copy(ptr, byteArr, 0, size);

                    mem = new MemoryStream();
                    BinaryWriter wtr = new BinaryWriter(mem);
                    int count = BitConverter.ToUInt16(byteArr, 4);
                    int offset = 6 + (0x10 * count);
                    wtr.Write(byteArr, 0, 6);
                    for (int i = 0; i < count; i++)
                    {
                        wtr.BaseStream.Seek(6 + (0x10 * i), SeekOrigin.Begin);
                        wtr.Write(byteArr, 6 + (14 * i), 12);
                        wtr.Write(offset);
                        IntPtr id = (IntPtr)BitConverter.ToUInt16(byteArr, (6 + (14 * i)) + 12);

                        IntPtr icoRes = FindResource(hMod, id, 3);
                        IntPtr icoDat = LoadResource(hMod, icoRes);
                        IntPtr icoPtr = LockResource(icoDat);
                        int icoSize = SizeofResource(hMod, icoRes);
                        byte[] img = new byte[icoSize];
                        Marshal.Copy(icoPtr, img, 0, icoSize);

                        wtr.BaseStream.Seek(offset, SeekOrigin.Begin);
                        wtr.Write(img, 0, img.Length);
                        offset += img.Length;
                    }
                    return false;
                }
                return true;
            }), IntPtr.Zero);
            FreeLibrary(hMod);
            if (mem == null) return null;
            BitmapImage ret = new BitmapImage();
            ret.BeginInit();
            ret.StreamSource = mem;
            ret.EndInit();
            return ret;
        }

        public static T FindChild<T>(DependencyObject parent, string childName)
            where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = (T)child;
                        break;
                    }
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else
                {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }
    }
}
