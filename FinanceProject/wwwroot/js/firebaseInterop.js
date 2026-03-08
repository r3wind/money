window.firebaseInterop = {

    onAuthStateChanged: function (dotNetHelper) {
        firebase.auth().onAuthStateChanged(function (user) {
            var userInfo = user
                ? { uid: user.uid, email: user.email, displayName: user.displayName }
                : null;
            dotNetHelper.invokeMethodAsync('OnAuthStateChanged', userInfo);
        });
    },

    signInWithEmail: async function (email, password) {
        try {
            await firebase.auth().signInWithEmailAndPassword(email, password);
            return null;
        } catch (e) {
            return e.message;
        }
    },

    registerWithEmail: async function (email, password) {
        try {
            await firebase.auth().createUserWithEmailAndPassword(email, password);
            return null;
        } catch (e) {
            return e.message;
        }
    },

    signInWithGoogle: async function () {
        try {
            var provider = new firebase.auth.GoogleAuthProvider();
            await firebase.auth().signInWithPopup(provider);
            return null;
        } catch (e) {
            return e.message;
        }
    },

    signOut: function () {
        return firebase.auth().signOut();
    },

    firestoreSave: async function (key, jsonData) {
        var user = firebase.auth().currentUser;
        if (!user) throw new Error('Not authenticated');
        await firebase.firestore()
            .collection('users').doc(user.uid)
            .collection('finance').doc(key)
            .set({ data: jsonData });
    },

    firestoreLoad: async function (key) {
        var user = firebase.auth().currentUser;
        if (!user) return null;
        var doc = await firebase.firestore()
            .collection('users').doc(user.uid)
            .collection('finance').doc(key)
            .get();
        return doc.exists ? doc.data().data : null;
    }

};
