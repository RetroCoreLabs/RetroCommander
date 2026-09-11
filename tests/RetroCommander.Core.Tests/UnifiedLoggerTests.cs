//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroCommander.Core.Tests -- Unit tests for UnifiedLogger
//

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RetroCommander.Core.Logging;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Unit tests for UnifiedLogger.
    /// Tests file logging, log rotation, and concurrent logging.
    /// </summary>
    [TestFixture]
    public class UnifiedLoggerTests
    {
        private string _testLogDirectory = null!;
        private string _testLogFile = null!;

        [SetUp]
        public void Setup()
        {
            // Create unique test directory for each test
            _testLogDirectory = Path.Combine(Path.GetTempPath(), "RetroCommanderLogTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testLogDirectory);
            _testLogFile = Path.Combine(_testLogDirectory, "test.log");
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test directory
            if (Directory.Exists(_testLogDirectory))
            {
                try
                {
                    Directory.Delete(_testLogDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Basic Logging Tests

        [Test]
        public void LogInfo_WritesToFile_FileContainsMessage()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogInfo("Test info message");
                // Give background writer time to process
                Thread.Sleep(200);
            }

            // Assert
            Assert.That(File.Exists(_testLogFile), Is.True, "Log file should exist");
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Test info message"));
            Assert.That(content, Does.Contain("[Info]"));
        }

        [Test]
        public void LogError_WritesToFile_FileContainsErrorLevel()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogError("Test error message");
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Test error message"));
            Assert.That(content, Does.Contain("[Error]"));
        }

        [Test]
        public void LogWarning_WritesToFile_FileContainsWarningLevel()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogWarning("Test warning message");
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Test warning message"));
            Assert.That(content, Does.Contain("[Warning]"));
        }

        [Test]
        public void LogDebug_WithDebugLevel_WritesToFile()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogDebug("Debug message");
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Debug message"));
            Assert.That(content, Does.Contain("[Debug]"));
        }

        [Test]
        public void LogDebug_WithInfoLevel_DoesNotWriteToFile()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info // Only Info and above
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogDebug("Debug message that should not appear");
                Thread.Sleep(200);
            }

            // Assert - file may not exist or be empty since no messages meet threshold
            if (File.Exists(_testLogFile))
            {
                var content = File.ReadAllText(_testLogFile);
                Assert.That(content, Does.Not.Contain("Debug message that should not appear"));
            }
        }

        [Test]
        public void LogCritical_WritesToFile_FileContainsCriticalLevel()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogCritical("Critical failure");
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Critical failure"));
            Assert.That(content, Does.Contain("[Critical]"));
        }

        [Test]
        public void LogTrace_WithTraceLevel_WritesToFile()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Trace
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogTrace("Trace message");
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Trace message"));
            Assert.That(content, Does.Contain("[Trace]"));
        }

        #endregion

        #region Log Format Tests

        [Test]
        public void Log_ContainsTimestamp_TimestampFormatCorrect()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogInfo("Timestamp test");
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            // Should contain timestamp in format: yyyy-MM-dd HH:mm:ss.fff
            Assert.That(content, Does.Match(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}"));
        }

        [Test]
        public void LogError_WithException_ContainsExceptionDetails()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                try
                {
                    throw new InvalidOperationException("Test exception message");
                }
                catch (Exception ex)
                {
                    logger.LogError("Error occurred", ex);
                }
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Error occurred"));
            Assert.That(content, Does.Contain("InvalidOperationException"));
            Assert.That(content, Does.Contain("Test exception message"));
        }

        [Test]
        public void LogOperation_ContainsOperationName()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogOperation("FileCopy", "Copying file.txt to destination");
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("FileCopy"));
            Assert.That(content, Does.Contain("Copying file.txt to destination"));
        }

        [Test]
        public void LogFileOperation_ContainsFilePath()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogFileOperation("Read", @"C:\test\file.txt");
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Read"));
            Assert.That(content, Does.Contain(@"C:\test\file.txt"));
        }

        [Test]
        public void LogTransfer_Success_ContainsTransferDetails()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogTransfer(@"C:\source\file.txt", @"D:\dest\file.txt", 1024, true);
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Transfer completed"));
            Assert.That(content, Does.Contain("1,024 bytes"));
        }

        [Test]
        public void LogTransfer_Failure_ContainsErrorLevel()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogTransfer(@"C:\source\file.txt", @"D:\dest\file.txt", 0, false);
                Thread.Sleep(200);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Transfer failed"));
            Assert.That(content, Does.Contain("[Error]"));
        }

        #endregion

        #region Log Rotation Tests

        [Test]
        public void LogRotation_WhenFileExceeds10MB_CreatesArchiveFile()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            // Create a file slightly over 10MB to trigger rotation
            var largeContent = new string('X', 10 * 1024 * 1024 + 1000); // 10MB + 1KB
            File.WriteAllText(_testLogFile, largeContent);

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogInfo("Message after rotation trigger");
                Thread.Sleep(500); // Allow rotation to complete
            }

            // Assert
            var files = Directory.GetFiles(_testLogDirectory, "*.old");
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(1), "Should have created archive file");
        }

        [Test]
        public void LogRotation_ArchiveFileName_ContainsTimestamp()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            // Create a file over 10MB
            var largeContent = new string('X', 10 * 1024 * 1024 + 1000);
            File.WriteAllText(_testLogFile, largeContent);

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogInfo("Trigger rotation");
                Thread.Sleep(500);
            }

            // Assert
            var archiveFiles = Directory.GetFiles(_testLogDirectory, "*.old");
            if (archiveFiles.Length > 0)
            {
                // Archive filename should contain timestamp: yyyyMMdd-HHmmss
                Assert.That(archiveFiles[0], Does.Match(@"\d{8}-\d{6}\.old$"));
            }
        }

        [Test]
        public void LogRotation_AfterRotation_ArchiveFileContainsOldContent()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            // Create a file over 10MB with identifiable content
            var largeContent = "ORIGINAL_CONTENT_MARKER" + new string('X', 10 * 1024 * 1024);
            File.WriteAllText(_testLogFile, largeContent);

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogInfo("Trigger rotation");
                Thread.Sleep(1000); // Allow rotation to complete
            }

            // Assert
            // After rotation, there should be archive file(s) with the original content
            var archiveFiles = Directory.GetFiles(_testLogDirectory, "*.old");
            Assert.That(archiveFiles.Length, Is.GreaterThanOrEqualTo(1), "Should have archive file after rotation");

            // Verify the archive contains the original content
            var archiveContent = File.ReadAllText(archiveFiles[0]);
            Assert.That(archiveContent, Does.Contain("ORIGINAL_CONTENT_MARKER"),
                "Archive file should contain original content");
        }

        #endregion

        #region Concurrent Logging Tests

        [Test]
        public void ConcurrentLogging_MultipleThreads_AllMessagesLogged()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            const int threadCount = 5;
            const int messagesPerThread = 20;
            var barrier = new Barrier(threadCount);

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                var tasks = new Task[threadCount];

                for (int t = 0; t < threadCount; t++)
                {
                    int threadId = t;
                    tasks[t] = Task.Run(() =>
                    {
                        barrier.SignalAndWait(); // Synchronize start

                        for (int i = 0; i < messagesPerThread; i++)
                        {
                            logger.LogInfo($"Thread{threadId:D2}-Message{i:D3}");
                        }
                    });
                }

                Task.WaitAll(tasks);
                Thread.Sleep(2000); // Allow background writer to complete
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Should have all messages (threadCount * messagesPerThread = 100)
            int expectedMessages = threadCount * messagesPerThread;
            Assert.That(lines.Length, Is.GreaterThanOrEqualTo(expectedMessages),
                $"Expected at least {expectedMessages} log lines, got {lines.Length}");
        }

        [Test]
        public void ConcurrentLogging_MultipleLogLevels_AllLevelsLogged()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Trace
            };

            const int iterations = 20;
            var barrier = new Barrier(5);

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                var tasks = new Task[5];

                tasks[0] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (int i = 0; i < iterations; i++)
                        logger.LogTrace($"Trace-{i}");
                });

                tasks[1] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (int i = 0; i < iterations; i++)
                        logger.LogDebug($"Debug-{i}");
                });

                tasks[2] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (int i = 0; i < iterations; i++)
                        logger.LogInfo($"Info-{i}");
                });

                tasks[3] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (int i = 0; i < iterations; i++)
                        logger.LogWarning($"Warning-{i}");
                });

                tasks[4] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    for (int i = 0; i < iterations; i++)
                        logger.LogError($"Error-{i}");
                });

                Task.WaitAll(tasks);
                Thread.Sleep(1000);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("[Trace]"));
            Assert.That(content, Does.Contain("[Debug]"));
            Assert.That(content, Does.Contain("[Info]"));
            Assert.That(content, Does.Contain("[Warning]"));
            Assert.That(content, Does.Contain("[Error]"));
        }

        [Test]
        public void ConcurrentLogging_NoDataCorruption_LinesAreComplete()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            const int threadCount = 5;
            const int messagesPerThread = 100;

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                var tasks = new Task[threadCount];

                for (int t = 0; t < threadCount; t++)
                {
                    int threadId = t;
                    tasks[t] = Task.Run(() =>
                    {
                        for (int i = 0; i < messagesPerThread; i++)
                        {
                            logger.LogInfo($"MARKER_START_T{threadId}_M{i}_MARKER_END");
                        }
                    });
                }

                Task.WaitAll(tasks);
                Thread.Sleep(1000);
            }

            // Assert - check for data corruption (incomplete markers)
            var content = File.ReadAllText(_testLogFile);
            var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            int completeMarkers = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains("MARKER_START") && line.Contains("MARKER_END"))
                {
                    completeMarkers++;
                }
            }

            // All messages should have complete markers
            Assert.That(completeMarkers, Is.EqualTo(threadCount * messagesPerThread),
                "All log lines should have complete markers (no data corruption)");
        }

        [Test]
        public void ConcurrentLogging_WithExceptions_HandledGracefully()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Debug
            };

            const int threadCount = 4;
            const int messagesPerThread = 25;

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                var tasks = new Task[threadCount];

                for (int t = 0; t < threadCount; t++)
                {
                    int threadId = t;
                    tasks[t] = Task.Run(() =>
                    {
                        for (int i = 0; i < messagesPerThread; i++)
                        {
                            try
                            {
                                throw new Exception($"Test exception from thread {threadId}");
                            }
                            catch (Exception ex)
                            {
                                logger.LogError($"Thread{threadId}-Exception{i}", ex);
                            }
                        }
                    });
                }

                Task.WaitAll(tasks);
                Thread.Sleep(1000);
            }

            // Assert
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("Exception"));
            Assert.That(content, Does.Contain("Test exception from thread"));

            // File should not be corrupted
            var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.GreaterThan(0));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Logger_DisposeTwice_DoesNotThrow()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act & Assert
            var logger = new UnifiedLogger(config);
            logger.LogInfo("Test message");
            Thread.Sleep(100);

            Assert.DoesNotThrow(() =>
            {
                logger.Dispose();
                logger.Dispose();
            });
        }

        [Test]
        public void Logger_CreatesDirectoryIfNotExists()
        {
            // Arrange
            var nestedDir = Path.Combine(_testLogDirectory, "nested", "deep", "path");
            var nestedLogFile = Path.Combine(nestedDir, "test.log");

            var config = new LoggingConfiguration
            {
                LogFilePath = nestedLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogInfo("Test in nested directory");
                Thread.Sleep(200);
            }

            // Assert
            Assert.That(Directory.Exists(nestedDir), Is.True, "Nested directory should be created");
            Assert.That(File.Exists(nestedLogFile), Is.True, "Log file should exist in nested directory");
        }

        [Test]
        public void Logger_EmptyMessage_LogsSuccessfully()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogInfo("");
                Thread.Sleep(200);
            }

            // Assert
            Assert.That(File.Exists(_testLogFile), Is.True);
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain("[Info]"));
        }

        [Test]
        public void Logger_VeryLongMessage_LogsSuccessfully()
        {
            // Arrange
            var config = new LoggingConfiguration
            {
                LogFilePath = _testLogFile,
                FileLogLevel = LogLevel.Info
            };

            var longMessage = new string('A', 100000); // 100KB message

            // Act
            using (var logger = new UnifiedLogger(config))
            {
                logger.LogInfo(longMessage);
                Thread.Sleep(500);
            }

            // Assert
            Assert.That(File.Exists(_testLogFile), Is.True);
            var content = File.ReadAllText(_testLogFile);
            Assert.That(content, Does.Contain(longMessage));
        }

        #endregion
    }
}
