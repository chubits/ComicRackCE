using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using cYo.Common.Collections;
using cYo.Common.Localize;
using cYo.Common.Reflection;
using cYo.Common.Threading;

namespace cYo.Projects.ComicRack.Engine.IO.Provider
{
	public class ProviderFactory<T> where T : class
	{
		private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

		private readonly List<ProviderInfo> providerDict = new List<ProviderInfo>();

		public void RegisterProvider(Type pt, IEnumerable<FileFormat> formats, bool withLocking = true)
		{
			using (withLocking ? rwLock.UpgradeableReadLock() : null)
			{
				if (!providerDict.Any((ProviderInfo pi) => pi.ProviderType == pt))
				{
					using (withLocking ? rwLock.WriteLock() : null)
					{
						providerDict.Add(new ProviderInfo(pt, formats));
					}
				}
			}
		}

		public void RegisterProvider(Type pt, bool withLocking = true)
		{
			IValidateProvider validateProvider = Activator.CreateInstance(pt) as IValidateProvider;
			if (validateProvider == null || validateProvider.IsValid)
			{
				RegisterProvider(pt, from ffa in pt.GetAttributes<FileFormatAttribute>()
					select ffa.Format, withLocking);
			}
		}

		public void RegisterProviders(Assembly assembly, Type baseType)
		{
			using (rwLock.WriteLock())
			{
				(from t in assembly.GetTypes()
					where !t.IsAbstract && t.IsSubclassOf(baseType) && t.GetConstructor(new Type[0]) != null
					select t).ForEach(delegate(Type t)
				{
					RegisterProvider(t, withLocking: false);
				});
			}
		}

		public void RegisterProviders(Assembly assembly)
		{
			RegisterProviders(assembly, typeof(T));
		}

		public void RegisterProviders()
		{
			RegisterProviders(Assembly.GetExecutingAssembly());
		}

		public IEnumerable<ProviderInfo> GetProviderInfos()
		{
			return providerDict.ReadLock(rwLock);
		}

		public IEnumerable<Type> GetProviderTypes()
		{
			return from pi in GetProviderInfos()
				select pi.ProviderType;
		}

		public IEnumerable<ProviderInfo> GetSourceProviderInfos(string source)
		{
			return from pi in GetProviderInfos()
				where pi.Formats.Any((FileFormat f) => f.Supports(source))
				select pi;
		}

		public IEnumerable<Type> GetSourceProviderTypes(string source)
		{
			return from f in GetSourceProviderInfos(source)
				select f.ProviderType;
		}

		public ProviderInfo GetSourceProviderInfo(string source)
		{
			return GetSourceProviderInfos(source).FirstOrDefault();
		}

		public Type GetSourceProviderType(string source)
		{
			return GetSourceProviderTypes(source).FirstOrDefault();
		}

		public IEnumerable<FileFormat> GetSourceFormats()
		{
			return GetProviderInfos().SelectMany((ProviderInfo pi) => pi.Formats);
		}

		public IEnumerable<FileFormat> GetSourceFormats(string source)
		{
			return GetSourceProviderInfos(source).SelectMany((ProviderInfo pi) => pi.Formats);
		}

		public virtual FileFormat GetSourceFormat(string source)
		{
			return GetSourceFormats(source).FirstOrDefault((FileFormat ff) => ff.Supports(source));
		}

		public string GetSourceFormatName(string source)
		{
			FileFormat sourceFormat = GetSourceFormat(source);
			if (sourceFormat != null && !string.IsNullOrEmpty(sourceFormat.Name))
			{
				return sourceFormat.Name;
			}
			return TR.Default["Unknown", "Unknown"];
		}

		public Type GetFormatProviderType(string formatName)
		{
			return (from pi in GetProviderInfos()
				where pi.Formats.Any((FileFormat f) => f.Name == formatName)
				select pi.ProviderType).FirstOrDefault();
		}

		public Type GetFormatProviderType(int formatId)
		{
			return (from pi in GetProviderInfos()
				where pi.Formats.Any((FileFormat f) => f.Id == formatId)
				select pi.ProviderType).FirstOrDefault();
		}

		public IEnumerable<string> GetFileExtensions()
		{
			return GetSourceFormats().SelectMany((FileFormat f) => f.Extensions).Distinct();
		}

		public T CreateFormatProvider(string formatName)
		{
			try
			{
				return Activator.CreateInstance(GetFormatProviderType(formatName)) as T;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public T CreateFormatProvider(int formatId)
		{
			Type formatProviderType = GetFormatProviderType(formatId);
			if (!(formatProviderType != null))
			{
				return null;
			}
			return Activator.CreateInstance(formatProviderType) as T;
		}

		public IEnumerable<T> CreateProviders()
		{
			return from t in GetProviderTypes()
				select Activator.CreateInstance(t) as T;
		}

		public virtual T CreateSourceProvider(string source)
		{
			try
			{
				return Activator.CreateInstance(GetSourceProviderType(source)) as T;
			}
			catch
			{
				return null;
			}
		}

		public string GetDialogFilter(bool withAllFilter, bool sort)
		{
			IEnumerable<FileFormat> enumerable = GetSourceFormats();
			if (sort)
			{
				enumerable = enumerable.OrderBy((FileFormat f) => f);
			}
			return enumerable.GetDialogFilter(withAllFilter);
		}
	}
}
