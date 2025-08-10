Обов'язково покласти зірочки на ці два репозиторії

book-cat - додаток на React (то кинулось якось погано, https://github.com/OksanaMykytyn/book-cat-react ось тут він є)
BookCatApi - апі на c#

У файлі BookCatAPI\appsettings.json є змінна, що відповідає за базу даних. Це DefaultConnection.

У файлі BookCatAPI\Program.cs є змінна, яка відповідає за приймання запитів з React.

У React тепер є глобальна змінна для запитів. Вона знаходиться у src\axiosInstance.js. Її назва - API_BASE_URL

АЛЕ!!! Поміняти localhost для зображень в InWaitingUser, UserCard, "UserChat", Header, HeaderAdmin, "MessageItem", AllDocumentPage, PendingUser, ArticleCard, renderContent, ArticlePage
