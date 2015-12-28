// ImageListView - A listview control for image files
// Copyright (C) 2009 Ozgur Ozcitak
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Ozgur Ozcitak (ozcitak@yahoo.com)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using ImageGlass.Common;
using ImageGlass.Core;

namespace ImageGlass.ImageListView
{
	/// <summary>
	/// Represents the cache manager responsible for asynchronously loading
	/// shell info.
	/// </summary>
	class ImageListViewCacheShellInfo : ISTAgentQueue, IDisposable {
		private readonly ImageListView mImageListView;

		private readonly Dictionary<string, CacheItem> shellCache;

	    readonly TaskFactory uiFactory;
        readonly SingleThreadAgent dispatchAgent;

	    /// <summary>
		/// Represents an item in the cache.
		/// </summary>
		private class CacheItem : IDisposable {
			/// <summary>
			/// Gets the file extension.
			/// </summary>
			public string Extension { get; }
			/// <summary>
			/// Gets the small shell icon.
			/// </summary>
			public Image SmallIcon { get; }
			/// <summary>
			/// Gets the large shell icon.
			/// </summary>
			public Image LargeIcon { get; }
			/// <summary>
			/// Gets the shell file type.
			/// </summary>
			public string FileType { get; }
			/// <summary>
			/// Gets or sets the state of the cache item.
			/// </summary>
			public CacheState State { get; set; }

			/// <summary>
			/// Initializes a new instance of the <see cref="CacheItem"/> class.
			/// </summary>
			/// <param name="extension">The file extension.</param>
			/// <param name="smallIcon">The small shell icon.</param>
			/// <param name="largeIcon">The large shell icon.</param>
			/// <param name="filetype">The shell file type.</param>
			/// <param name="state">The cache state of the item.</param>
			public CacheItem (string extension, Image smallIcon, Image largeIcon, string filetype, CacheState state)
			{
				Extension = extension;
				SmallIcon = smallIcon;
				LargeIcon = largeIcon;
				FileType = filetype;
				State = state;
			}

			/// <summary>
			/// Performs application-defined tasks associated with 
			/// freeing, releasing, or resetting unmanaged resources.
			/// </summary>
			public void Dispose ()
			{
                SmallIcon?.Dispose ();
                LargeIcon?.Dispose ();
#if DEBUG
                GC.SuppressFinalize(this);
#endif
            }
#if DEBUG
			/// <summary>
			/// Releases unmanaged resources and performs other cleanup operations before the
			/// CacheItem is reclaimed by garbage collection.
			/// </summary>
			~CacheItem () {
                System.Diagnostics.Debug.Print ("Finalizer of {0} called for non-empty cache item.", GetType ());
			}
#endif
		}

	    /// <summary>
		/// Determines whether the cache manager retries loading items on errors.
		/// </summary>
		public bool RetryOnError { get; internal set; }

	    #region Constructor
		/// <summary>
		/// Initializes a new instance of the <see cref="ImageListViewCacheShellInfo"/> class.
		/// </summary>
		/// <param name="owner">The owner control.</param>
		public ImageListViewCacheShellInfo (ImageListView owner) {
            Contract.Requires(owner != null);
            Contract.Requires(SynchronizationContext.Current != null);

            uiFactory = new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext());
            dispatchAgent = new SingleThreadAgent(this);
			
			mImageListView = owner;
			RetryOnError = false;
			
			shellCache = new Dictionary<string, CacheItem> ();
		}
		/// <summary>
		/// Performs application-defined tasks associated with freeing,
		/// releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose () => Clear();
		#endregion

		#region ISTAgentQueue
        readonly ConcurrentQueue<string> requests = new ConcurrentQueue<string>();
	    bool ISTAgentQueue.HasWork => requests.Count > 0;
	    Option<Action> ISTAgentQueue.GetWorkItem(){
	        string item;
	        return requests.TryDequeue(out item)
                ? Option<Action>.Some(() => RunItem(item))
                : (Option<Action>) Option<Action>.None();
	    }
	    void RunItem(string extension){
	        CacheItem existing;
	        if (shellCache.TryGetValue(extension, out existing) && existing.SmallIcon != null && existing.LargeIcon != null)
	            return;

	        var info = ShellInfoExtractor.FromFile(extension);

	        CacheItem result;
	        if ((info.SmallIcon == null || info.LargeIcon == null) && !RetryOnError)
	            result = new CacheItem(extension, info.SmallIcon, info.LargeIcon, info.FileType, CacheState.Error);
	        else
	            result = new CacheItem(extension, info.SmallIcon, info.LargeIcon, info.FileType, CacheState.Cached);

	        // Add to cache
	        if (shellCache.TryGetValue(result.Extension, out existing)){
	            existing.Dispose();
	            shellCache.Remove(result.Extension);
	        }
	        shellCache.Add(result.Extension, result);

	        uiFactory.StartNew(() => mImageListView.Refresh(false, true));
	    }

	    #endregion

		#region Instance Methods

	    /// <summary>
	    /// Pauses the cache threads. 
	    /// </summary>
	    public void Pause() => dispatchAgent.Pause();
	    /// <summary>
	    /// Resumes the cache threads. 
	    /// </summary>
	    public void Resume() => dispatchAgent.Resume();
		/// <summary>
		/// Gets the cache state of the specified item.
		/// </summary>
		/// <param name="extension">File extension.</param>
		public CacheState GetCacheState (string extension)
		{
			if (string.IsNullOrEmpty (extension))
				throw new ArgumentException ("extension cannot be null", "extension");
			
			CacheItem item;
			if (shellCache.TryGetValue (extension, out item))
				return item.State;
			
			return CacheState.Unknown;
		}
		/// <summary>
		/// Rebuilds the cache.
		/// Old items will be kept until they are overwritten
		/// by new ones.
		/// </summary>
		public void Rebuild ()
		{
			foreach (CacheItem item in shellCache.Values)
				item.State = CacheState.Unknown;
		}
		/// <summary>
		/// Clears the cache.
		/// </summary>
		public void Clear ()
		{
			foreach (CacheItem item in shellCache.Values)
				item.Dispose ();
			shellCache.Clear ();
		}
		/// <summary>
		/// Removes the given item from the cache.
		/// </summary>
		/// <param name="extension">File extension.</param>
		public void Remove (string extension)
		{
			if (string.IsNullOrEmpty (extension))
				throw new ArgumentException ("extension cannot be null", "extension");
			
			CacheItem item;
			if (shellCache.TryGetValue (extension, out item)) {
				item.Dispose ();
				shellCache.Remove (extension);
			}
		}
		/// <summary>
		/// Adds the item to the cache queue.
		/// </summary>
		/// <param name="extension">File extension.</param>
		public void Add (string extension)
		{
			if (string.IsNullOrEmpty (extension))
				throw new ArgumentException ("extension cannot be null", "extension");
			
			// Already cached?
			CacheItem item;
			if (shellCache.TryGetValue (extension, out item))
				return;
			
			// Add to cache queue
			RunWorker (extension);
		}
		/// <summary>
		/// Gets the small shell icon for the given file extension from the cache.
		/// If the item is not cached, null will be returned.
		/// </summary>
		/// <param name="extension">File extension.</param>
		public Image GetSmallIcon (string extension)
		{
			if (string.IsNullOrEmpty (extension))
				throw new ArgumentException ("extension cannot be null", "extension");
			
			CacheItem item;
			if (shellCache.TryGetValue (extension, out item)) {
				return item.SmallIcon;
			}
			return null;
		}
		/// <summary>
		/// Gets the large shell icon for the given file extension from the cache.
		/// If the item is not cached, null will be returned.
		/// </summary>
		/// <param name="extension">File extension.</param>
		public Image GetLargeIcon (string extension)
		{
			if (string.IsNullOrEmpty (extension))
				throw new ArgumentException ("extension cannot be null", "extension");
			
			CacheItem item;
			if (shellCache.TryGetValue (extension, out item)) {
				return item.LargeIcon;
			}
			return null;
		}
		/// <summary>
		/// Gets the shell file type for the given file extension from the cache.
		/// If the item is not cached, null will be returned.
		/// </summary>
		/// <param name="extension">File extension.</param>
		public string GetFileType (string extension)
		{
			if (string.IsNullOrEmpty (extension))
				throw new ArgumentException ("extension cannot be null", "extension");
			
			CacheItem item;
			if (shellCache.TryGetValue (extension, out item)) {
				return item.FileType;
			}
			return null;
		}
		#endregion

		#region RunWorker
		/// <summary>
		/// Pushes the given item to the worker queue.
		/// </summary>
		/// <param name="extension">File extension.</param>
		private void RunWorker (string extension) {
            requests.Enqueue(extension);
            dispatchAgent.Schedule();
		}
		#endregion
	}
}
