using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData.IO
{
    class FileSurgeon
    {
        private readonly FileStructureHelper structureHelper;
        private readonly IMetaDataEmbedder embedder;

        private readonly int implementedTagType;
        private readonly int defaultTagOffset;

        public delegate WriteResult WriteDelegate(BinaryWriter w, TagData tag, Zone zone);


        // Modes for file modification
        public enum WriteMode
        {
            MODE_REPLACE = 0,   // Replace : existing block is replaced by written data
            MODE_OVERWRITE = 1  // Overwrite : written data overwrites existing block (non-overwritten parts are kept as is)
        }

        public class WriteResult
        {
            public readonly WriteMode RequiredMode;
            public readonly int WrittenFields;

            public WriteResult(WriteMode requiredMode, int writtenFields)
            {
                RequiredMode = requiredMode;
                WrittenFields = writtenFields;
            }
        }


        public FileSurgeon(
            FileStructureHelper structureHelper,
            IMetaDataEmbedder embedder,
            int implementedTagType,
            int defaultTagOffset)
        {
            this.structureHelper = structureHelper;
            this.embedder = embedder;
            this.implementedTagType = implementedTagType;
            this.defaultTagOffset = defaultTagOffset;
        }


        public bool RewriteZones(
            BinaryWriter w,
            WriteDelegate write,
            ICollection<Zone> zones,
            TagData dataToWrite,
            bool tagExists)
        {
            long oldTagSize;
            long newTagSize;
            long cumulativeDelta = 0;
            bool result = true;

            foreach (Zone zone in zones)
            {
                oldTagSize = zone.Size;

                // Write new tag to a MemoryStream
                using (MemoryStream s = new MemoryStream(zone.Size))
                using (BinaryWriter msw = new BinaryWriter(s, Settings.DefaultTextEncoding))
                {
                    dataToWrite.DataSizeDelta = cumulativeDelta;
                    WriteResult writeResult = write(msw, dataToWrite, zone);

                    if (WriteMode.MODE_REPLACE == writeResult.RequiredMode)
                    {
                        if (writeResult.WrittenFields > 0)
                        {
                            newTagSize = s.Length;

                            if (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2 && embedder.ID3v2EmbeddingHeaderSize > 0)
                            {
                                StreamUtils.LengthenStream(s, 0, embedder.ID3v2EmbeddingHeaderSize);
                                s.Position = 0;
                                embedder.WriteID3v2EmbeddingHeader(msw, newTagSize);

                                newTagSize = s.Length;
                            }
                        }
                        else
                        {
                            newTagSize = zone.CoreSignature.Length;
                        }
                    }
                    else // Overwrite mode
                    {
                        newTagSize = zone.Size;
                    }

                    // -- Adjust tag slot to new size in file --
                    long tagBeginOffset, tagEndOffset;

                    if (tagExists && zone.Size > zone.CoreSignature.Length) // An existing tag has been reprocessed
                    {
                        tagBeginOffset = zone.Offset + cumulativeDelta;
                        tagEndOffset = tagBeginOffset + zone.Size;
                    }
                    else // A brand new tag has been added to the file
                    {
                        if (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2)
                        {
                            tagBeginOffset = embedder.Id3v2Zone.Offset;
                        }
                        else
                        {
                            switch (defaultTagOffset)
                            {
                                case MetaDataIO.TO_EOF: tagBeginOffset = w.BaseStream.Length; break;
                                case MetaDataIO.TO_BOF: tagBeginOffset = 0; break;
                                case MetaDataIO.TO_BUILTIN: tagBeginOffset = zone.Offset + cumulativeDelta; break;
                                default: tagBeginOffset = -1; break;
                            }
                        }
                        tagEndOffset = tagBeginOffset + zone.Size;
                    }

                    if (WriteMode.MODE_REPLACE == writeResult.RequiredMode)
                    {
                        // Need to build a larger file
                        if (newTagSize > zone.Size)
                        {
                            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Data stream operation : Lengthening (delta=" + (newTagSize - zone.Size) + ")");
                            StreamUtils.LengthenStream(w.BaseStream, tagEndOffset, (uint)(newTagSize - zone.Size));
                        }
                        else if (newTagSize < zone.Size) // Need to reduce file size
                        {
                            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Data stream operation : Shortening (delta=" + (newTagSize - zone.Size) + ")");
                            StreamUtils.ShortenStream(w.BaseStream, tagEndOffset, (uint)(zone.Size - newTagSize));
                        }
                    }

                    // Copy tag contents to the new slot
                    w.BaseStream.Seek(tagBeginOffset, SeekOrigin.Begin);
                    s.Seek(0, SeekOrigin.Begin);

                    if (writeResult.WrittenFields > 0)
                    {
                        StreamUtils.CopyStream(s, w.BaseStream);
                    }
                    else
                    {
                        if (zone.CoreSignature.Length > 0) msw.Write(zone.CoreSignature);
                    }

                    long delta = newTagSize - oldTagSize;
                    cumulativeDelta += delta;

                    // Edit wrapping size markers and frame counters if needed
                    if (structureHelper != null && delta != 0 && (MetaDataIOFactory.TAG_NATIVE == implementedTagType || (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2)))
                    {
                        int action;
                        bool isTagWritten = (writeResult.WrittenFields > 0);

                        if (oldTagSize == zone.CoreSignature.Length && isTagWritten) action = ACTION_ADD;
                        else if (newTagSize == zone.CoreSignature.Length && !isTagWritten) action = ACTION_DELETE;
                        else action = ACTION_EDIT;

                        result = structureHelper.RewriteHeaders(w, delta, action, zone.Name);
                    }

                    zone.Size = (int)newTagSize;
                }
            } // Loop through zones

            return result;
        }
    }
}
