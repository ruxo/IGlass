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
using System.Threading;
using System.Threading.Tasks;
using ImageGlass.Common;
using ImageGlass.Common.Extensions;
using ImageGlass.Core;

namespace ImageGlass.ImageListView
{
	/// <summary>
	/// Represents the cache manager responsible for asynchronously loading
	/// item metadata.
	/// </summary>
	class ImageListViewCacheMetadata : ISTAgentQueue, IDisposable {
	    readonly ImageListView mImageListView;

	    readonly Dictionary<Guid, bool> editCache;
	    readonly Dictionary<Guid, bool> processing;
	    readonly Dictionary<Guid, bool> removedItems;
        readonly ConcurrentQueue<CacheRequest> requests = new ConcurrentQueue<CacheRequest>(); 
        readonly SingleThreadAgent dispatchAgent;
	    readonly TaskFactory uiFactory;

	    /// <summary>
		/// Represents a cache request.
		/// </summary>
	    class CacheRequest
		{
			/// <summary>
			/// Gets the item guid.
			/// </summary>
			public Guid Guid { get; private set; }
			/// <summary>
			/// Gets the adaptor of this item.
			/// </summary>
			public ImageListView.ImageListViewItemAdaptor Adaptor { get; private set; }
			/// <summary>
			/// Gets the virtual item key.
			/// </summary>
			public object VirtualItemKey { get; private set; }
			/// <summary>
			/// Whether to use the Windows Imaging Component.
			/// </summary>
			public bool UseWIC { get; private set; }

			/// <summary>
			/// Initializes a new instance of the <see cref="CacheRequest"/> class.
			/// </summary>
			/// <param name="guid">The guid of the item.</param>
			/// <param name="adaptor">The adaptor of this item.</param>
			/// <param name="virtualItemKey">The virtual item key of this item.</param>
			/// <param name="useWIC">Whether to use the Windows Imaging Component.</param>
			public CacheRequest (Guid guid, ImageListView.ImageListViewItemAdaptor adaptor, object virtualItemKey, bool useWIC)
			{
				Guid = guid;
				Adaptor = adaptor;
				VirtualItemKey = virtualItemKey;
				UseWIC = useWIC;
			}
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
		public ImageListViewCacheMetadata (ImageListView owner) {
            Contract.Requires(owner != null);
            Contract.Requires(SynchronizationContext.Current != null);

            uiFactory = new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext());
            dispatchAgent = new SingleThreadAgent(this);

			mImageListView = owner;
			RetryOnError = false;
			
			editCache = new Dictionary<Guid, bool> ();
			processing = new Dictionary<Guid, bool> ();
			removedItems = new Dictionary<Guid, bool> ();
		}
	    /// <summary>
	    /// Performs application-defined tasks associated with freeing,
	    /// releasing, or resetting unmanaged resources.
	    /// </summary>
	    public void Dispose (){
	        dispatchAgent.Clear(() => requests.Clear());
        }

	    #endregion

	    #region Async dispatch queue

	    bool ISTAgentQueue.HasWork => requests.Count > 0;
	    Option<Action> ISTAgentQueue.GetWorkItem(){
	        CacheRequest item;
	        return requests.TryDequeue(out item)
	            ? Option<Action>.Some(() => RunItem(item))
	            : (Option<Action>) Option<Action>.None();
	    }
	    public void ClearQueue() => requests.Clear();
	    void RunItem(CacheRequest item){
	        if (ShouldProcess(item)){
	            var result = Either<Exception, Utility.Tuple<ColumnType, string, object>[]>.SafeDo(() => item.Adaptor.GetDetails(item.VirtualItemKey, item.UseWIC));

	            result
	                .Do(e => mImageListView.OnCacheErrorInternal(item.Guid, e, CacheThread.Details),
	                    details => uiFactory.StartNew(() =>{
	                        if (details != null)
	                            mImageListView.UpdateItemDetailsInternal(item.Guid, details);

	                        // Refresh the control lazily
	                        if (mImageListView.IsItemVisible(item.Guid))
	                            mImageListView.Refresh(false, true);
	                    })
	                );
	        }
	    }

        bool ShouldProcess(CacheRequest request) => !editCache.ContainsKey(request.Guid) && mImageListView.IsItemDirty(request.Guid);

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
		/// Starts editing an item. While items are edited,
		/// the cache thread will not work on them to prevent collisions.
		/// </summary>
		/// <param name="guid">The guid representing the item</param>
		public void BeginItemEdit (Guid guid)
		{
			if (!editCache.ContainsKey (guid))
				editCache.Add (guid, false);
		}
		/// <summary>
		/// Ends editing an item. After this call, item
		/// image will be continued to be fetched by the thread.
		/// </summary>
		/// <param name="guid">The guid representing the item.</param>
		public void EndItemEdit (Guid guid) => editCache.Remove (guid);
	    /// <summary>
		/// Removes the given item from the cache.
		/// </summary>
		/// <param name="guid">The guid of the item to remove.</param>
		public void Remove (Guid guid)
		{
			if (!removedItems.ContainsKey (guid))
				removedItems.Add (guid, false);
		}
		/// <summary>
		/// Clears the cache.
		/// </summary>
		public void Clear () => processing.Clear ();
	    /// <summary>
		/// Adds the item to the cache queue.
		/// </summary>
		/// <param name="guid">Item guid.</param>
		/// <param name="adaptor">The adaptor for this item.</param>
		/// <param name="virtualItemKey">The virtual item key.</param>
		/// <param name="useWIC">Whether to use the Windows Imaging Component.</param>
		public void Add (Guid guid, ImageListView.ImageListViewItemAdaptor adaptor, object virtualItemKey, bool useWIC) {
			RunWorker (new CacheRequest (guid, adaptor, virtualItemKey, useWIC));
		}
		#endregion

	    /// <summary>
		/// Pushes the given item to the worker queue.
		/// </summary>
		/// <param name="item">The cache item.</param>
		void RunWorker (CacheRequest item) {
			// Already being processed?
		    if (processing.ContainsKey(item.Guid))
		        return;
			else
				processing.Add (item.Guid, false);

            requests.Enqueue(item);
            dispatchAgent.Schedule();
		}
	}
}
