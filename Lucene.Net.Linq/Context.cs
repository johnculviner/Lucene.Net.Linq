﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Linq.Abstractions;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;

namespace Lucene.Net.Linq
{
    internal class Context
    {
        public event EventHandler<SearcherLoadEventArgs> SearcherLoading;

        private readonly Directory directory;
        private readonly Analyzer analyzer;
        private readonly Version version;
        private readonly IIndexWriter indexWriter;
        private readonly object transactionLock;

        private readonly object searcherLock = new object();
        private readonly object reloadLock = new object();
        private SearcherClientTracker tracker;
        
        public Context(Directory directory, Analyzer analyzer, Version version, IIndexWriter indexWriter, object transactionLock)
        {
            this.directory = directory;
            this.analyzer = analyzer;
            this.version = version;
            this.indexWriter = indexWriter;
            this.transactionLock = transactionLock;
        }

        public Analyzer Analyzer
        {
            get { return analyzer; }
        }

        public Version Version
        {
            get { return version; }
        }

        public Directory Directory
        {
            get { return directory; }
        }

        public IIndexWriter IndexWriter
        {
            get { return indexWriter; }
        }

        public object TransactionLock
        {
            get { return transactionLock; }
        }

        public ISearcherHandle CheckoutSearcher()
        {
            return new SearcherHandle(CurrentTracker);
        }

        public virtual void Reload()
        {
            lock (reloadLock)
            {
                var newTracker = new SearcherClientTracker(CreateSearcher());

                var tmpHandler = SearcherLoading;

                if (tmpHandler != null)
                {
                    tmpHandler(this, new SearcherLoadEventArgs(newTracker.Searcher));
                }

                lock (searcherLock)
                {
                    if (tracker != null)
                    {
                        tracker.Dispose();
                    }

                    tracker = newTracker;
                }
            }
        }

        internal SearcherClientTracker CurrentTracker
        {
            get
            {
                lock (searcherLock)
                {
                    if (tracker == null)
                    {
                        tracker = new SearcherClientTracker(CreateSearcher());
                    }
                    return tracker;
                }
            }
        }

        public bool IsReadOnly
        {
            get { return IndexWriter ==  null; }
        }

        protected virtual IndexSearcher CreateSearcher()
        {
            return new IndexSearcher(directory, true);
        }

        internal class SearcherHandle : ISearcherHandle
        {
            private readonly SearcherClientTracker tracker;
            private bool disposed;

            public SearcherHandle(SearcherClientTracker tracker)
            {
                this.tracker = tracker;
                tracker.AddClient(this);
            }

            public IndexSearcher Searcher
            {
                get { return tracker.Searcher; }
            }

            public void Dispose()
            {
                if (disposed) throw new ObjectDisposedException(typeof(ISearcherHandle).Name);
                disposed = true;
                tracker.RemoveClient(this);
            }
        }

        internal class SearcherClientTracker : IDisposable
        {
            private static readonly IList<SearcherClientTracker> undisposedTrackers = new List<SearcherClientTracker>();

            private readonly object sync = new object();
            private readonly List<WeakReference> searcherReferences = new List<WeakReference>();
            private readonly IndexSearcher searcher;
            private bool disposePending;
            private bool disposed;

            public SearcherClientTracker(IndexSearcher searcher)
            {
                this.searcher = searcher;

                lock(typeof(SearcherClientTracker))
                {
                    undisposedTrackers.Add(this);
                }
            }

            public IndexSearcher Searcher
            {
                get { return searcher; }
            }

            public void AddClient(object client)
            {
                lock (sync)
                    searcherReferences.Add(new WeakReference(client));
            }

            public void RemoveClient(object client)
            {
                lock (sync)
                {
                    searcherReferences.Remove(searcherReferences.First(wr => ReferenceEquals(wr.Target, client)));
                    RemoveDeadReferences();

                    if (disposePending)
                    {
                        Dispose();
                    }
                }
            }

            public void Dispose()
            {
                lock (sync)
                {
                    disposePending = false;

                    if (disposed)
                    {
                        throw new ObjectDisposedException(GetType().Name);
                    }

                    RemoveDeadReferences();
                    if (searcherReferences.Count == 0)
                    {
                        lock (typeof(SearcherClientTracker))
                        {
                            undisposedTrackers.Remove(this);
                        }

                        searcher.Dispose();
                        disposed = true;
                    }
                    else
                    {
                        disposePending = true;
                    }
                }
            }

            internal int ReferenceCount
            {
                get
                {
                    lock (sync) return searcherReferences.Count;
                }
            }

            private void RemoveDeadReferences()
            {
                searcherReferences.RemoveAll(wr => !wr.IsAlive);
            }
        }
    }

    internal interface ISearcherHandle : IDisposable
    {
        IndexSearcher Searcher { get; }
    }

}