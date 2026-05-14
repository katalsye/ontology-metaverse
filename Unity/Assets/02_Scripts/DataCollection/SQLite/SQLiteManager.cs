using System;
using System.IO;
using UnityEngine;
using SQLite;

namespace OntologyMetaverse.DataCollection.SQLite
{
    /// <summary>
    /// SQLite 데이터베이스 매니저
    /// 4개 테이블의 초기화, 연결, 기본 CRUD 제공
    /// </summary>
    public class SQLiteManager : IDisposable
    {
        private const string DB_FILE_NAME = "ontology_metaverse.db";
        
        private SQLiteConnection _db;
        private static SQLiteManager _instance;
        
        /// <summary>
        /// 싱글톤 인스턴스
        /// </summary>
        public static SQLiteManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SQLiteManager();
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// DB 파일 경로
        /// Android: /data/data/[패키지명]/files/ontology_metaverse.db
        /// </summary>
        public string DbPath => Path.Combine(Application.persistentDataPath, DB_FILE_NAME);
        
        /// <summary>
        /// 생성자: DB 연결 및 테이블 생성
        /// </summary>
        private SQLiteManager()
        {
            try
            {
                _db = new SQLiteConnection(DbPath);
                CreateTables();
                Debug.Log($"[SQLiteManager] DB 초기화 완료: {DbPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SQLiteManager] DB 초기화 실패: {e.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 4개 테이블 자동 생성 (이미 존재 시 무시)
        /// </summary>
        private void CreateTables()
        {
            _db.CreateTable<RawData>();
            _db.CreateTable<Triple>();
            _db.CreateTable<ImageCache>();
            _db.CreateTable<InferenceQueueItem>();
            Debug.Log("[SQLiteManager] 4개 테이블 생성/확인 완료");
        }
        
        /// <summary>
        /// 외부에서 직접 SQLite 연결에 접근 (고급 쿼리용)
        /// </summary>
        public SQLiteConnection Connection => _db;
        
        /// <summary>
        /// DB 연결 종료
        /// </summary>
        public void Dispose()
        {
            _db?.Close();
            _db = null;
            _instance = null;
            Debug.Log("[SQLiteManager] DB 연결 종료");
        }
    }
}