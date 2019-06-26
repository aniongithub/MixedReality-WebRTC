// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a remote video source added as a video track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The video track can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Remote Video Source")]
    public class RemoteVideoSource : VideoSource
    {
        /// <summary>
        /// Peer connection this remote video source is extracted from.
        /// </summary>
        [Header("Video track")]
        public PeerConnection PeerConnection;

        /// <summary>
        /// Automatically play the remote video track when it is added.
        /// </summary>
        public bool AutoPlayOnAdded = true;

        /// <summary>
        /// Internal queue used to marshal work back to the main Unity thread.
        /// </summary>
        private ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();

        protected void Awake()
        {
            FrameQueue = new VideoFrameQueue<I420VideoFrameStorage>(5);
            PeerConnection.OnInitialized.AddListener(OnPeerInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerShutdown);
        }

        protected void OnDestroy()
        {
            PeerConnection.OnInitialized.RemoveListener(OnPeerInitialized);
            PeerConnection.OnShutdown.RemoveListener(OnPeerShutdown);
        }

        protected void Update()
        {
            // Execute any pending work enqueued by background tasks
            while (_mainThreadWorkQueue.TryDequeue(out Action workload))
            {
                workload();
            }
        }

        private void OnPeerInitialized()
        {
            if (AutoPlayOnAdded)
            {
                PeerConnection.Peer.TrackAdded += TrackAdded;
                PeerConnection.Peer.TrackRemoved += TrackRemoved;
                PeerConnection.Peer.I420RemoteVideoFrameReady += I420RemoteVideoFrameReady;
            }
        }

        private void TrackAdded()
        {
            // Enqueue invoking the unity event from the main Unity thread, so that listeners
            // can directly access Unity objects from their handler function.
            _mainThreadWorkQueue.Enqueue(() => VideoStreamStarted.Invoke());
        }

        private void TrackRemoved()
        {
            // Enqueue invoking the unity event from the main Unity thread, so that listeners
            // can directly access Unity objects from their handler function.
            _mainThreadWorkQueue.Enqueue(() => VideoStreamStopped.Invoke());
        }

        private void OnPeerShutdown()
        {
            PeerConnection.Peer.I420RemoteVideoFrameReady -= I420RemoteVideoFrameReady;
        }

        private void I420RemoteVideoFrameReady(I420AVideoFrame frame)
        {
            // This does not need to enqueue work, because FrameQueue is thread-safe
            // and can be manipulated from any thread (does not access Unity objects).
            FrameQueue.Enqueue(frame);
        }
    }
}
