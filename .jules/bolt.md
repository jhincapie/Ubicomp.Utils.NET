## 2024-05-23 - ThreadPool Starvation in Message Resequencing
**Learning:** The `TransportComponent` was using `Monitor.Wait` to block ThreadPool threads while waiting for out-of-order UDP packets. In scenarios with high packet loss or reordering, this could exhaust the ThreadPool, causing a "thundering herd" effect and application hang.
**Action:** Replaced blocking waits with a non-blocking buffering strategy (`Dictionary` + Loop) to ensure strict ordering without holding threads. Always prefer asynchronous buffering or state machines over blocking synchronization for message resequencing.
