using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

public class Config : MonoBehaviour
{
	public int DefaultFontSize = 14;
	public float DefaultWidthPerLevel = 27;
	public float DefaultHeightPerLine = 27.0f;

	public bool DoBackUp = true;

	public int LineHeapCount = 30;
	public int FontSize = 14;
	public float WidthFactor = 1.0f;
	public float HeightFactor = 1.0f;
	public float WidthPerLevel { get { return DefaultWidthPerLevel * WidthFactor * (float)FontSize / DefaultFontSize; } }
	public float HeightPerLine { get { return DefaultHeightPerLine * HeightFactor * (float)FontSize / DefaultFontSize; } }
	public float AnimTime = 0.05f;
	public float AnimOvershoot = 1.70158f;
	public float ArrowStreamDelayTime = 0.3f;
	public float ArrowStreamIntervalTime = 0.03f;
	public float TextInputFixIntervalTime = 1.0f;
	public float DoubleClickInterval = 0.25f;
	public float AutoSaveTime = 1.5f;
	public int LogLoadUnit = 7;
	public int NumRecentFilesMenu = 9;

	public float TagLineHeight = 30;
	public float TagListWidth = 200;
	public float TagListTextMaxWidth = 140;
	public float TabTextMaxWidth = 140;
	public float LogNoteHeaderMargin = 30;
	public int LogNoteSetPathCoroutineCount = 7;
	public float LogNoteSetPathCoroutineInterval = 0.03f;

	public Color ThemeColor;
	public Color AccentColor;
	public Color DoneColor;
	public Color DiaryColor;

	public Color SelectionColor;
	public Color TextColor;
	public Color StrikeColor;
	public Color DoneTextColor;
	public Color CloneTextColor;
	public Color CommentLineColor;
	public Color CommentTextColor;
	public Color TagSelectionColor;
	public Color TagSubTextColor;

	public Color ToggleColor;
	public Color ToggleOpenedColor;

	public Color SearchFocusColor;
	public Color SearchUnfocusColor;

	public Color IconColor;

	public string TimeFormat = "HH:mm";
	public string DateFormat = "yyyy/M/d (ddd)";
	public string DefaultTag = "todo";

	#region InitialTreeText
	public string InitialTreeText = @"Welcome to Dones
	階層化
		Tab, Shift+Tab
			階層化
				階層化の解除
					子要素も一緒についてきます。
				どのカーソル位置でTabを押してもTab文字挿入はされず、階層化になります。
		Ctrl+↑↓
			折りたたみ、折りたたみ解除<f>
				子要素
				子要素
				子要素
			←の▼を押しても折りたたみ可能
				子要素
				子要素
			Ctrl+Shift+↑↓で、子要素を含めた全体を折りたたみ、折りたたみ解除可能<f>
				子要素<f>
					孫要素<f>
						曾孫要素
					孫要素<f>
						曾孫要素
		Alt+↑↓
			行の入れ替え
			上の行
				（入れ替えは同じレベル同士で行われる）
			下の行
	
	タスク管理
		Ctrl+Space
			Done<d>
			もう一度入力でDone解除
			タスク
				→親要素をDoneしても、子要素のDoneは無関係。
				コメントを書いてもいいし、
				サブタスクを定義しても良い
		Ctrl+Shift+Space
			Repeat Done
			ログにDoneされた状態で記録されるが、
			繰り返しDoneできるようにもとに戻る
	
	ログ
		Doneされたものは、親子構造を含めて↓のログに記録されます。
			例：ここをDoneすると
				子要素に書いていたコメントやタスクもログに記録されます。
		ログについて
			ログに直接書き込んで、日誌のように使うこともできます。
			デフォルトでは今日のDoneのみを表示しており、最小化、最大化も可能です。
			最大化すると、過去のDone記録を順次スクロールして閲覧・編集することができます。
		Ctrl+L
			ログの表示状態を最小化、デフォルト、最大化の順番で切り替えます。
	
	タブ
		Ctrl+E
			任意の行から下だけを抜き出してタブ化できます。
			（右クリックでコンテキストメニューを開いて一番上の「新しいタブで開く」からも可能です）
		タブ化のTIPS
			プロジェクトごとや、よく使う項目などをタブ化しておくと便利です。
			UndoやRedo履歴はタブによって表示されている範囲の動作のみを対象とします。
			ツリーの一番上に現在のタブまでのパスが表示されます。
			パスをクリックすることで別階層にも移動できます。
			親階層で折りたたまれていても別タブで開くことが可能です。
			ログの表示も、タブで開かれている階層のDone結果のみになります。
			※ただし、ログが多くなってくると切り替えが重たいので、そのうち対策します。
		Ctrl+PageUp,PageDown
			上下のTabに切り替えます
	
	タグ
		行末に半角スペース＋半角＃でタグをつけることができます。
		やること #todo
		#を入力した時点でタグ候補から選択も可能です。
		タグリストについて
			マウスで右上の＃ボタンをクリックしてタグ付けされた行を一覧できます。
			左端の□をクリックして、タスクをDoneにすることもできます。
			ドラッグして順位を入れ替えたり別のタグに移動したりもできます。
			ダブルクリックしたらその行を表示します。
			現在のタブで表示されていない行はタグリストにも表示されません。
		タグのPinとRepeat
			タグの右側にある「…」をクリックすると、以下の設定が可能です。
			Pin:常に表示
				そのタグに含まれる行がなくなっても、タグリストに常に表示しておくようにできます。
			Repeat:繰り返し
				Doneボタンを押した時にRepeatDone（ログにだけ記録して、もう一度Doneできる状態に戻る）します。
		Ctrl+D
			タグを自動挿入します。
			連続で押すとタグが切り替わっていきます。
			タグリストに表示されているものが順番に出てきます。
		Ctrl+T
			タグリストを開閉します。
	
	その他
		一般的なショートカットは普通に使えます
		Ctrl+F
			検索
			F3またはEnterで次の検索結果に移動、Shift押しながらF3,Enterで前の結果に移動
		Ctrl+C,V,X
			コピペ
		Ctrl+Z,Y
			Undo,Redo
		Ctrl+Shift+C
			形式（Done状態などの属性）や折りたたまれた行を含めずにコピー
		Ctrl+A
			全選択
		Ctrl+Home,End
			一番上または下の行にジャンプ
		F5
			ファイルをリロードします。
			表示がおかしくなってやべぇ！という時の緊急避難に使えます。
		URL
			https://twitter.com/i/moments/898858277189992448
			リンクを貼りつけてクリックできます。（行の途中からはだめで、行頭がhttpで始まる場合のみ判定しています）
		Ctrl+:
			時間を入力します
			13:40
		Ctrl+;
			日付を入力します
			2017/6/29 (木)
	
	仕様
		オートセーブです
			右上にsaving...という表示が出ます
		１行単位でのアクションが基本
			右端まで書いても折り返したりしません。１行のまま続きます。
			行をまたぐ選択は１行単位での選択になります。
		ファイル
			C:\Users\（ユーザー名）\AppData\Roaming\Dones
			に、ツリーのファイルやログファイル、設定ファイルが保存されています。
			別環境で共有したい場合
				将来的にはクラウドにしたいですが、未実装です。
				settings.xmlにある
				<SaveDirectory>パス</SaveDirectory>
				を編集すると、保存先を変えることができるので、
				これを各自DropBoxなりに変更していただくと複数環境で同じツリーにアクセスできます。
				ただし、同時に開かれても同期をとっているわけではないので
				現状はミスって上書きとかに注意しながら使ってください。
	
	既知のバグ
		選択時の青い表示が出ない時があります。選択し直すとすぐ出てきます。よくわかっていません。
		階層を表す矢印や棒が最初だけ表示されない時があります。スクロールしたりすると出てきます。これもよくわかっていません。
		Tabが極稀に効かなくなるときがあります。やばいです。アプリ再起動しかないかも。条件が分かり次第直します。
		テクスチャアトラスの再構成時のバグで画面全体が文字化けというのを喰らいましたが、おそらく直っているはずです（発生したら教えてください）
	";
	#endregion

	FileInfo configFile_;
	ConfigXML configXml_;

	// Use this for initialization
	void Awake()
	{
		GameContext.Config = this;
	}

	// Update is called once per frame
	void Update()
	{

	}

	void OnValidate()
	{
		AnimInfoBase.overshoot = AnimOvershoot;
	}

	void OnApplicationQuit()
	{
		SaveConfig();
	}

	#region config save / load

	[XmlRoot("config")]
	public class ConfigXML
	{
		[XmlElement("TimeFormat")]
		public string TimeFormat { get; set; }

		[XmlElement("DateFormat")]
		public string DateFormat { get; set; }

		[XmlElement("FontSize")]
		public int FontSize { get; set; }
	}

	public void LoadConfig(string filepath)
	{
		configFile_ = new FileInfo(filepath);

		if( configFile_.Exists == false )
		{
			return;
		}

		XmlSerializer serializer = new XmlSerializer(typeof(ConfigXML));
		StreamReader reader = new StreamReader(configFile_.FullName);
		configXml_ = (ConfigXML)serializer.Deserialize(reader);
		reader.Close();

		TimeFormat = configXml_.TimeFormat;
		DateFormat = configXml_.DateFormat;
		FontSize = configXml_.FontSize;
	}

	void SaveConfig()
	{
		if( configFile_.Exists == false )
		{
			if( Directory.Exists(configFile_.DirectoryName) == false )
			{
				Directory.CreateDirectory(configFile_.DirectoryName);
			}
		}
		
		ConfigXML config = new ConfigXML();

		config.TimeFormat	= TimeFormat;
		config.DateFormat	= DateFormat;
		config.FontSize		= FontSize;
		
		StreamWriter writer = new StreamWriter(configFile_.FullName);
		XmlSerializer serializer = new XmlSerializer(typeof(ConfigXML));
		serializer.Serialize(writer, config);
		writer.Flush();
		writer.Close();
	}

	#endregion
}
